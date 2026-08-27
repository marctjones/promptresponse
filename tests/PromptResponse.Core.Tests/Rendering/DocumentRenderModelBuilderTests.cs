using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using Xunit;

namespace PromptResponse.Core.Tests.Rendering;

/// <summary>
/// Verifies the single shared document traversal: nesting → heading levels,
/// prompts → fields, empty-field inclusion, and table flattening. These
/// invariants are what every output format (PDF, text, HTML) relies on.
/// </summary>
public class DocumentRenderModelBuilderTests
{
    private readonly DocumentRenderModelBuilder _builder = new();

    private static AprDocument Doc(params Section[] sections) => new()
    {
        Metadata = new Metadata { Title = "My Form", Description = "A test form" },
        Sections = sections.ToList(),
    };

    [Fact]
    public void Build_CarriesDocumentTitleAndDescription()
    {
        var model = _builder.Build(Doc(), RenderOptions.Default);

        model.Title.Should().Be("My Form");
        model.Description.Should().Be("A test form");
    }

    [Fact]
    public void Build_TopLevelSection_IsHeadingLevelOne()
    {
        var model = _builder.Build(
            Doc(new Section { Id = "s1", Title = "Personal" }),
            RenderOptions.Default);

        model.Blocks.Should().ContainSingle()
            .Which.Should().BeOfType<HeadingBlock>()
            .Which.Should().BeEquivalentTo(new { Level = 1, Text = "Personal" });
    }

    [Fact]
    public void Build_NestedSections_IncreaseHeadingLevel()
    {
        var doc = Doc(new Section
        {
            Id = "s1",
            Title = "Outer",
            Sections = [new Section { Id = "s1_1", Title = "Inner" }],
        });

        var model = _builder.Build(doc, RenderOptions.Default);

        var headings = model.Blocks.OfType<HeadingBlock>().ToList();
        headings.Should().HaveCount(2);
        headings[0].Should().BeEquivalentTo(new { Level = 1, Text = "Outer" });
        headings[1].Should().BeEquivalentTo(new { Level = 2, Text = "Inner" });
    }

    [Fact]
    public void Build_PromptsBecomeFields_InOrderAfterHeading()
    {
        var doc = Doc(new Section
        {
            Id = "s1",
            Title = "Contact",
            Prompts =
            [
                new Prompt { Id = "name", Label = "Name", Response = "Ada" },
                new Prompt { Id = "email", Label = "Email", Response = "ada@x.io" },
            ],
        });

        var model = _builder.Build(doc, RenderOptions.Default);

        model.Blocks[0].Should().BeOfType<HeadingBlock>();
        var fields = model.Blocks.OfType<FieldBlock>().ToList();
        fields.Should().HaveCount(2);
        fields[0].Should().BeEquivalentTo(new { Label = "Name", Value = "Ada", HasResponse = true });
        fields[1].Label.Should().Be("Email");
    }

    [Fact]
    public void Build_FieldCarriesHintsAndDataType()
    {
        var doc = Doc(new Section
        {
            Id = "s1",
            Title = "S",
            Prompts =
            [
                new Prompt
                {
                    Id = "dob",
                    Label = "Date of birth",
                    Hints = new PromptHints { HelpText = "As on ID", ExpectedDataType = "date" },
                },
            ],
        });

        var field = _builder.Build(doc, RenderOptions.Default).Blocks.OfType<FieldBlock>().Single();

        field.HasResponse.Should().BeFalse();
        field.Value.Should().Be(RenderOptions.Default.EmptyFieldText);
        field.HelpText.Should().Be("As on ID");
        field.ExpectedDataType.Should().Be("date");
    }

    [Fact]
    public void Build_Field_CarriesPromptIdAndSuggestedValuesAsChoices()
    {
        var doc = Doc(new Section
        {
            Id = "s1",
            Title = "S",
            Prompts =
            [
                new Prompt
                {
                    Id = "color",
                    Label = "Colour",
                    Hints = new PromptHints { SuggestedValues = ["Red", "Green"] },
                },
            ],
        });

        var field = _builder.Build(doc, RenderOptions.Default).Blocks.OfType<FieldBlock>().Single();

        field.Id.Should().Be("color");
        field.Choices.Should().Equal("Red", "Green");
    }

    [Fact]
    public void Build_Field_WithNoSuggestedValues_HasNullChoices()
    {
        var doc = Doc(new Section
        {
            Id = "s1", Title = "S",
            Prompts = [new Prompt { Id = "name", Label = "Name", Response = "x" }],
        });

        _builder.Build(doc, RenderOptions.Default).Blocks.OfType<FieldBlock>().Single()
            .Choices.Should().BeNull();
    }

    [Fact]
    public void Build_ExcludeEmptyFields_DropsUnansweredPrompts()
    {
        var doc = Doc(new Section
        {
            Id = "s1",
            Title = "S",
            Prompts =
            [
                new Prompt { Id = "a", Label = "Answered", Response = "yes" },
                new Prompt { Id = "b", Label = "Blank", Response = "" },
            ],
        });

        var model = _builder.Build(doc, new RenderOptions { IncludeEmptyFields = false });

        var fields = model.Blocks.OfType<FieldBlock>().ToList();
        fields.Should().ContainSingle().Which.Label.Should().Be("Answered");
    }

    [Fact]
    public void Build_IncludeEmptyFields_UsesPlaceholderForUnanswered()
    {
        var doc = Doc(new Section
        {
            Id = "s1",
            Title = "S",
            Prompts = [new Prompt { Id = "b", Label = "Blank", Response = "" }],
        });

        var field = _builder
            .Build(doc, new RenderOptions { IncludeEmptyFields = true, EmptyFieldText = "—" })
            .Blocks.OfType<FieldBlock>().Single();

        field.HasResponse.Should().BeFalse();
        field.Value.Should().Be("—");
    }

    [Fact]
    public void Build_TableSection_FlattensToTableBlock_WithHeadersAndCells()
    {
        var doc = Doc(new Section
        {
            Id = "tax",
            Title = "Tax years",
            Kind = "table",
            Sections =
            [
                new Section
                {
                    Id = "r2024",
                    Title = "2024",
                    Prompts =
                    [
                        new Prompt { Id = "r2024.year", Label = "Year", Response = "2024" },
                        new Prompt { Id = "r2024.revenue", Label = "Revenue", Response = "1000" },
                    ],
                },
            ],
        });

        var model = _builder.Build(doc, RenderOptions.Default);

        // A heading for the section, then exactly one table block (no recursion
        // into the row sub-section).
        model.Blocks.OfType<HeadingBlock>().Should().ContainSingle();
        var table = model.Blocks.OfType<TableBlock>().Should().ContainSingle().Subject;
        table.ColumnHeaders.Should().Equal("Year", "Revenue");
        table.Rows.Should().ContainSingle();
        table.Rows[0].Label.Should().Be("2024");
        table.Rows[0].Cells.Select(c => c.Value).Should().Equal("2024", "1000");
        table.Rows[0].Cells.Should().OnlyContain(c => c.HasResponse);
    }

    [Fact]
    public void Build_TableCells_CorrespondByPosition()
    {
        // Correspondence across instances is positional. There is no column record to
        // match ids against — a field's identity is "the nth prompt in each row", and
        // its header is that prompt's own label. Ids stay free-form and are for
        // addressing, not alignment.
        var doc = Doc(new Section
        {
            Id = "t",
            Title = "T",
            Kind = "table",
            Sections =
            [
                new Section
                {
                    Id = "row1",
                    Title = "Row 1",
                    Prompts =
                    [
                        new Prompt { Id = "row1.b", Label = "B", Response = "valB" },
                        new Prompt { Id = "row1.a", Label = "A", Response = "valA" },
                    ],
                },
                new Section
                {
                    Id = "row2",
                    Title = "Row 2",
                    Prompts =
                    [
                        new Prompt { Id = "row2.b", Label = "B", Response = "val2B" },
                        new Prompt { Id = "row2.a", Label = "A", Response = "val2A" },
                    ],
                },
            ],
        });

        var table = new DocumentRenderModelBuilder().Build(doc, RenderOptions.Default)
            .Blocks.OfType<TableBlock>().Single();

        table.ColumnHeaders.Should().Equal("B", "A");
        table.Rows[0].Cells.Select(c => c.Value).Should().Equal("valB", "valA");
        table.Rows[1].Cells.Select(c => c.Value).Should().Equal("val2B", "val2A");
    }

    [Fact]
    public void Build_TableCells_CarryIdAndColumnTypeAndChoices_ForFillableRendering()
    {
        var doc = Doc(new Section
        {
            Id = "t",
            Title = "T",
            Kind = "table",
            Sections =
            [
                new Section
                {
                    Id = "row1",
                    Title = "Row 1",
                    Prompts = [new Prompt { Id = "row1.amount", Label = "Amount", Response = "10",
                        Hints = new PromptHints { ExpectedDataType = "currency", SuggestedValues = ["Draft", "Final"] } }],
                },
            ],
        });

        var cells = _builder.Build(doc, RenderOptions.Default).Blocks.OfType<TableBlock>().Single().Rows[0].Cells;

        // A cell carries its id, type hint, and suggestions from the prompt itself —
        // there is no column record to read them from, so they cannot disagree.
        cells.Should().HaveCount(1, "a field exists only where a prompt exists; there are no phantom columns");
        cells[0].Id.Should().Be("row1.amount");
        cells[0].ExpectedDataType.Should().Be("currency");
        cells[0].Choices.Should().Equal("Draft", "Final");
    }

    [Fact]
    public void Build_SignedDocument_AppendsSignatureBlock()
    {
        var doc = Doc(new Section { Id = "s", Title = "S", Prompts = [new Prompt { Id = "a", Label = "A" }] });
        doc.Metadata.TemplateId = "t";
        using var cert = PromptResponse.Core.Signing.SignatureCertificates.CreateSelfSigned(
            "Publisher", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        doc.Signatures = [PromptResponse.Core.Signing.AprSigner.SignTemplate(doc, cert, DateTime.UtcNow)];

        var block = _builder.Build(doc, RenderOptions.Default).Blocks.OfType<SignatureBlock>().Single();

        block.Signatures.Should().ContainSingle();
        block.Signatures[0].Role.Should().Be("Publisher");
        block.Signatures[0].Signer.Should().Be("Publisher");
        block.Signatures[0].ContentValid.Should().BeTrue();
    }

    [Fact]
    public void Build_UnsignedDocument_HasNoSignatureBlock()
    {
        var doc = Doc(new Section { Id = "s", Title = "S", Prompts = [new Prompt { Id = "a", Label = "A" }] });
        _builder.Build(doc, RenderOptions.Default).Blocks.OfType<SignatureBlock>().Should().BeEmpty();
    }

    [Fact]
    public void Build_NullDocument_Throws()
    {
        var act = () => _builder.Build(null!, RenderOptions.Default);
        act.Should().Throw<ArgumentNullException>();
    }
}
