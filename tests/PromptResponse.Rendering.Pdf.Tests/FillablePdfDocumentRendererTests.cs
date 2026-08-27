using System.Text;
using AwesomeAssertions;
using NSubstitute;
using Pdfe.Core.Document;
using PdfeDoc = Pdfe.Core.Document.PdfDocument;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using Xunit;

namespace PromptResponse.Rendering.Pdf.Tests;

/// <summary>
/// Verifies the fillable-AcroForm renderer: prompts become live form fields
/// (text / checkbox / dropdown) named by prompt id, with responses as defaults.
/// Round-trip assertions reopen the output with Pdfe.Core and inspect the
/// AcroForm.
/// </summary>
public class FillablePdfDocumentRendererTests
{
    private readonly FillablePdfDocumentRenderer _renderer = new();

    private static AprDocument FormDoc() => new()
    {
        DocumentType = DocumentType.Template,
        Metadata = new Metadata { Title = "Sign-up" },
        Sections =
        [
            new Section
            {
                Id = "s1",
                Title = "Details",
                Prompts =
                [
                    new Prompt { Id = "name", Label = "Full Name", Response = "Ada" },
                    new Prompt
                    {
                        Id = "agree", Label = "I agree",
                        Hints = new PromptHints { ExpectedDataType = "boolean" },
                    },
                    new Prompt
                    {
                        Id = "color", Label = "Favourite colour",
                        Hints = new PromptHints { SuggestedValues = ["Red", "Green", "Blue"] },
                    },
                ],
            },
        ],
    };

    private static IReadOnlyList<PdfField> FieldsOf(byte[] bytes)
    {
        using var doc = PdfeDoc.Open(bytes);
        var form = doc.GetAcroForm();
        form.Should().NotBeNull("the rendered PDF should contain an AcroForm");
        return form!.Fields;
    }

    [Fact]
    public void FormatMetadata_IsPdfForm()
    {
        _renderer.FormatId.Should().Be("pdf-form");
        _renderer.FileExtension.Should().Be(".pdf");
    }

    [Fact]
    public void Render_ProducesValidPdf()
    {
        var bytes = _renderer.RenderToBytes(FormDoc());
        Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void Render_TextPrompt_BecomesTextFieldNamedByIdWithDefault()
    {
        var fields = FieldsOf(_renderer.RenderToBytes(FormDoc()));

        var name = fields.Should().ContainSingle(f => f.FullName == "name").Subject;
        name.FieldType.Should().Be(PdfFieldType.Text);
        name.Value.Should().Be("Ada");
    }

    [Fact]
    public void Render_BooleanPrompt_BecomesCheckbox()
    {
        var fields = FieldsOf(_renderer.RenderToBytes(FormDoc()));

        fields.Should().ContainSingle(f => f.FullName == "agree")
            .Which.FieldType.Should().Be(PdfFieldType.Button);
    }

    [Fact]
    public void Render_PromptWithSuggestedValues_BecomesDropdownWithOptions()
    {
        var fields = FieldsOf(_renderer.RenderToBytes(FormDoc()));

        var color = fields.Should().ContainSingle(f => f.FullName == "color").Subject;
        color.FieldType.Should().Be(PdfFieldType.Choice);
        color.Options.Should().Contain(["Red", "Green", "Blue"]);
    }

    [Fact]
    public void Render_IncludesEveryPrompt_EvenWithExcludeEmptyOption()
    {
        // A form needs all its blanks: empty-field option must not drop fields.
        var fields = FieldsOf(_renderer.RenderToBytes(FormDoc(), new RenderOptions { IncludeEmptyFields = false }));

        fields.Select(f => f.FullName).Should().Contain(["name", "agree", "color"]);
    }

    [Fact]
    public void Render_FixedTableCells_BecomeLiveFieldsNamedByCellId()
    {
        var doc = new AprDocument
        {
            DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "Quarterly" },
            Sections =
            [
                new Section
                {
                    Id = "q", Title = "By quarter",
                    Kind = "table",
                    Sections =
                    [
                        new Section { Id = "q1", Title = "Q1", Prompts =
                        [
                            new Prompt { Id = "q1.revenue", Label = "Revenue", Response = "5000" },
                            new Prompt { Id = "q1.audited", Label = "Audited", Response = "true", Hints = new PromptHints { ExpectedDataType = "boolean" } },
                            new Prompt { Id = "q1.status", Label = "Status", Response = "Final", Hints = new PromptHints { SuggestedValues = ["Draft", "Final"] } },
                        ]},
                    ],
                },
            ],
        };

        var fields = FieldsOf(_renderer.RenderToBytes(doc));

        var revenue = fields.Should().ContainSingle(f => f.FullName == "q1.revenue").Subject;
        revenue.FieldType.Should().Be(PdfFieldType.Text);
        revenue.Value.Should().Be("5000");

        fields.Should().ContainSingle(f => f.FullName == "q1.audited")
            .Which.FieldType.Should().Be(PdfFieldType.Button);

        var status = fields.Should().ContainSingle(f => f.FullName == "q1.status").Subject;
        status.FieldType.Should().Be(PdfFieldType.Choice);
        status.Options.Should().Contain(["Draft", "Final"]);

        // Accessible name combines the row + column headers.
        revenue.RawDictionary.GetStringOrNull("TU").Should().Be("Q1 Revenue");
    }

    [Fact]
    public void Render_GoesThroughTheSharedBuilder()
    {
        var builder = Substitute.For<IDocumentRenderModelBuilder>();
        builder.Build(Arg.Any<AprDocument>(), Arg.Any<RenderOptions>())
            .Returns(new RenderModel("T", null, DocumentType.Template,
                [new FieldBlock("Label", "V", true, null, null, "fld")]));
        var renderer = new FillablePdfDocumentRenderer(builder);

        var fields = FieldsOf(renderer.RenderToBytes(FormDoc()));

        builder.Received(1).Build(Arg.Any<AprDocument>(), Arg.Any<RenderOptions>());
        fields.Should().ContainSingle(f => f.FullName == "fld");
    }

    [Fact]
    public void Render_Fields_CarryAccessibleNames_ForScreenReaders()
    {
        // pdfe 2.5.0 defaults each field's /TU (the accessible name a screen
        // reader announces) to the visible label — so fillable forms are
        // accessible out of the box.
        var fields = FieldsOf(_renderer.RenderToBytes(FormDoc()));

        fields.Single(f => f.FullName == "name").RawDictionary.GetStringOrNull("TU")
            .Should().Be("Full Name");
        fields.Single(f => f.FullName == "agree").RawDictionary.GetStringOrNull("TU")
            .Should().Be("I agree");
    }

    [Fact]
    public void Render_NullArgs_Throw()
    {
        var act = () => _renderer.RenderToBytes(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// A response outside its offered choices must not break a fillable-PDF export.
    /// </summary>
    /// <remarks>
    /// Found by running the desktop app: typing a value into a table cell that offered
    /// suggestedValues and exporting a fillable PDF threw ArgumentException from the PDF
    /// layer ("Default value 'sf' is not one of the supplied options") and took the whole
    /// application down.
    ///
    /// Specification section 4.7 is explicit that suggestedValues offers options and a
    /// response outside the list is still valid, so this was the format's central rule
    /// crashing the app that implements it.
    /// </remarks>
    [Fact]
    public void FillableExport_AcceptsAResponseOutsideItsSuggestedValues()
    {
        var document = new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata { Title = "Choices", TemplateId = "t" },
            Sections =
            [
                new Section
                {
                    Id = "plain", Title = "Plain",
                    Prompts =
                    [
                        new Prompt
                        {
                            Id = "status", Label = "Status", Response = "Something else entirely",
                            Hints = new PromptHints { SuggestedValues = ["Draft", "Final"] },
                        },
                    ],
                },
                new Section
                {
                    Id = "tbl", Title = "Quarterly", Kind = "table",
                    Sections =
                    [
                        new Section
                        {
                            Id = "q1", Title = "Q1",
                            Prompts =
                            [
                                new Prompt { Id = "q1.revenue", Label = "Revenue", Response = "125000.00",
                                    Hints = new PromptHints { ExpectedDataType = "currency" } },
                                new Prompt { Id = "q1.status", Label = "Status", Response = "sf",
                                    Hints = new PromptHints { SuggestedValues = ["Draft", "Final"] } },
                            ],
                        },
                    ],
                },
            ],
        };

        using var output = new MemoryStream();
        var render = () => new FillablePdfDocumentRenderer().Render(document, RenderOptions.Default, output);

        render.Should().NotThrow("a response outside suggestedValues is valid and must still export");
        output.Length.Should().BeGreaterThan(0);
    }
}
