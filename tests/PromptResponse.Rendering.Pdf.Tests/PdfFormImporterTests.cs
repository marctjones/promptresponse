using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using PromptResponse.Core.Validation;
using Xunit;

namespace PromptResponse.Rendering.Pdf.Tests;

/// <summary>
/// Verifies the PDF AcroForm importer. Uses a round-trip: render a known APR to a
/// fillable PDF (which names each field by id and sets its tooltip to the label),
/// then import it back and assert the fields, labels, and types survived. Also
/// checks the flat-PDF (no AcroForm) path and that the result validates.
/// </summary>
public class PdfFormImporterTests
{
    private static AprDocument SourceForm() => new()
    {
        DocumentType = DocumentType.Template,
        Metadata = new Metadata { Title = "Sign-up" },
        Sections =
        [
            new Section
            {
                Id = "s1", Title = "Details",
                Prompts =
                [
                    new Prompt { Id = "full_name", Label = "Full Name", Hints = new PromptHints { ExpectedDataType = "text" } },
                    new Prompt { Id = "agree", Label = "I agree", Hints = new PromptHints { ExpectedDataType = "boolean" } },
                    new Prompt { Id = "color", Label = "Colour", Hints = new PromptHints { SuggestedValues = ["Red", "Green"] } },
                ],
            },
        ],
    };

    [Fact]
    public void Import_FillablePdf_RecoversFieldsAsPrompts()
    {
        var pdfBytes = new FillablePdfDocumentRenderer().RenderToBytes(SourceForm());

        var imported = new PdfFormImporter().Import(pdfBytes, "Sign-up");

        var prompts = imported.Sections.SelectMany(s => s.Prompts).ToList();
        prompts.Should().HaveCountGreaterThanOrEqualTo(3);

        // The fillable renderer sets /TU (tooltip) = the visible label, so the
        // importer recovers the human label rather than the raw field name.
        prompts.Select(p => p.Label).Should().Contain(["Full Name", "I agree", "Colour"]);

        prompts.Single(p => p.Label == "Full Name").Hints.ExpectedDataType.Should().Be("text");
        prompts.Single(p => p.Label == "I agree").Hints.ExpectedDataType.Should().Be("boolean");
        var colour = prompts.Single(p => p.Label == "Colour");
        colour.Hints.SuggestedValues.Should().Contain(["Red", "Green"]);
    }

    [Fact]
    public void Import_ProducesValidTemplate()
    {
        var pdfBytes = new FillablePdfDocumentRenderer().RenderToBytes(SourceForm());

        var imported = new PdfFormImporter().Import(pdfBytes, "Sign-up");

        imported.DocumentType.Should().Be(DocumentType.Template);
        imported.Sections.Should().NotBeEmpty();
        new DocumentValidator().Validate(imported).IsValid
            .Should().BeTrue("an imported form must be a structurally valid template");
    }

    [Fact]
    public void Import_AllResponsesAreBlank_ItIsATemplate()
    {
        var pdfBytes = new FillablePdfDocumentRenderer().RenderToBytes(SourceForm());

        var imported = new PdfFormImporter().Import(pdfBytes, "Sign-up");

        imported.Sections.SelectMany(s => s.Prompts).Select(p => p.Response)
            .Should().OnlyContain(r => r == string.Empty);
    }

    [Fact]
    public void Import_FlatPdf_ThrowsNoFormFields()
    {
        // A flat (non-fillable) PDF has no AcroForm to import.
        var flatBytes = new PdfDocumentRenderer().RenderToBytes(SourceForm());

        var act = () => new PdfFormImporter().Import(flatBytes, "Sign-up");

        act.Should().Throw<PdfFormImporter.NoFormFieldsException>();
    }
}
