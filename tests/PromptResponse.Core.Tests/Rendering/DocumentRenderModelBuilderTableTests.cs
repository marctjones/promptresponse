using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using Xunit;

namespace PromptResponse.Core.Tests.Rendering;

/// <summary>
/// Verifies table flattening and the field policy shared by table cells.
/// </summary>
public class DocumentRenderModelBuilderTableTests
{
    private readonly DocumentRenderModelBuilder _builder = DocumentRenderModelBuilderTestFactory.CreateBuilder();

    [Fact]
    public void Build_TableSection_FlattensToTableBlock_WithHeadersAndCells()
    {
        var doc = DocumentRenderModelBuilderTestFactory.CreateDocument(new Section
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
        var doc = DocumentRenderModelBuilderTestFactory.CreateDocument(new Section
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

        var table = _builder.Build(doc, RenderOptions.Default).Blocks.OfType<TableBlock>().Single();

        table.ColumnHeaders.Should().Equal("B", "A");
        table.Rows[0].Cells.Select(c => c.Value).Should().Equal("valB", "valA");
        table.Rows[1].Cells.Select(c => c.Value).Should().Equal("val2B", "val2A");
    }

    [Fact]
    public void Build_TableCells_CarryIdAndColumnTypeAndChoices_ForFillableRendering()
    {
        var doc = DocumentRenderModelBuilderTestFactory.CreateDocument(new Section
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

        cells.Should().HaveCount(1, "a field exists only where a prompt exists; there are no phantom columns");
        cells[0].Id.Should().Be("row1.amount");
        cells[0].ExpectedDataType.Should().Be("currency");
        cells[0].Choices.Should().Equal("Draft", "Final");
    }

    [Fact]
    public void Build_TableAndOrdinaryFields_UseTheSameBlankAndHintPolicy()
    {
        var hints = new PromptHints
        {
            HelpText = "   ",
            ExpectedDataType = "  ",
            SuggestedValues = ["Draft", "Final"],
        };
        var doc = DocumentRenderModelBuilderTestFactory.CreateDocument(
            new Section
            {
                Id = "ordinary",
                Title = "Ordinary",
                Prompts = [new Prompt { Id = "ordinary.status", Label = "Status", Response = "   ", Hints = hints }],
            },
            new Section
            {
                Id = "table",
                Title = "Table",
                Kind = "table",
                Sections =
                [
                    new Section
                    {
                        Id = "row",
                        Title = "Row",
                        Prompts = [new Prompt { Id = "row.status", Label = "Status", Response = "   ", Hints = hints }],
                    },
                ],
            });

        var model = _builder.Build(doc, RenderOptions.Default);
        var field = model.Blocks.OfType<FieldBlock>().Single();
        var cell = model.Blocks.OfType<TableBlock>().Single().Rows.Single().Cells.Single();

        field.HasResponse.Should().BeFalse();
        field.HelpText.Should().BeNull();
        field.ExpectedDataType.Should().BeNull();
        field.Choices.Should().Equal("Draft", "Final");
        cell.HasResponse.Should().BeFalse();
        cell.ExpectedDataType.Should().BeNull();
        cell.Choices.Should().Equal("Draft", "Final");
    }
}
