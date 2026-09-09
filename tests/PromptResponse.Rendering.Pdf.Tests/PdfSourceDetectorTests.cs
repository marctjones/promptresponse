using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using Xunit;

namespace PromptResponse.Rendering.Pdf.Tests;

/// <summary>
/// Which field sources a PDF offers decides everything downstream, and guessing
/// wrong is expensive in both directions: treat a digitally-authored form as a
/// scan and you spend OCR and a model re-deriving text the file already stated
/// exactly; treat a scan as text-bearing and you convert an empty document.
/// </summary>
public class PdfSourceDetectorTests
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
                ],
            },
        ],
    };

    [Fact]
    public void AFillableExport_OffersBothSources()
    {
        var pdf = new FillablePdfDocumentRenderer().RenderToBytes(SourceForm());

        var report = PdfSourceDetector.Detect(pdf);

        report.HasAcroForm.Should().BeTrue("the fillable renderer writes real AcroForm widgets");
        report.HasTextLayer.Should().BeTrue("it also prints the labels beside those widgets");
        report.CanRecoverLabelsFromText.Should().BeTrue(
            "both sources present is the case worth detecting: the AcroForm gives complete " +
            "field identity, the text layer gives the labels those fields often lack");
        report.IsImageOnly.Should().BeFalse();
        report.ImportableFieldCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AFlatExport_OffersTextButNoFormFields()
    {
        // The same document through the non-fillable renderer: printed, not interactive.
        var pdf = new PdfDocumentRenderer().RenderToBytes(SourceForm());

        var report = PdfSourceDetector.Detect(pdf);

        report.HasAcroForm.Should().BeFalse("a flat export has no AcroForm to read");
        report.HasTextLayer.Should().BeTrue(
            "but its text is still exact and positioned — which is why this case does not need OCR");
        report.ImportableFieldCount.Should().Be(0);
        report.IsImageOnly.Should().BeFalse(
            "no form fields is not the same as no information; calling this image-only " +
            "would send a perfectly readable document to OCR");
    }

    [Fact]
    public void AScannedFormSomebodyMadeFillable_HasFieldsButNoTextToLabelThemFrom()
    {
        // CT DMV A-25 is the case a binary "scanned or not?" classifier gets wrong.
        // It is a scan -- 0 extractable characters, where every other form in the
        // corpus yields thousands -- but somebody laid real AcroForm widgets over
        // the image, so it has 10 importable fields. Both facts are true at once.
        var report = PdfSourceDetector.Detect(ScannedCorpusForm());

        report.HasAcroForm.Should().BeTrue("real widgets were placed over the scanned image");
        report.ImportableFieldCount.Should().BeGreaterThan(0);
        report.HasTextLayer.Should().BeFalse("the printed content is pixels, not text");
        report.CharacterCount.Should().BeLessThan(PdfSourceDetector.MinCharactersForTextLayer);

        report.IsImageOnly.Should().BeFalse(
            "having no text layer is not the same as having nothing to work with");
        report.CanRecoverLabelsFromText.Should().BeFalse(
            "this is why the distinction earns its keep: apr import will find all ten " +
            "fields, but if their names are cryptic there is no text layer to recover " +
            "labels from -- only OCR can help, which is a different and costlier path");
    }

    [Fact]
    public void EveryPageIsReportedSeparately_WithTheEvidenceBehindItsVerdict()
    {
        var pdf = new FillablePdfDocumentRenderer().RenderToBytes(SourceForm());

        var report = PdfSourceDetector.Detect(pdf);

        report.Pages.Should().NotBeEmpty();
        report.Pages.Select(p => p.PageNumber).Should().BeInAscendingOrder()
            .And.OnlyHaveUniqueItems();
        report.Pages.Should().AllSatisfy(p => p.PageNumber.Should().BePositive("page numbers are 1-based"));
        report.CharacterCount.Should().Be(report.Pages.Sum(p => p.CharacterCount),
            "the document total has to be the sum of its pages, or the evidence is not checkable");
        report.ImportableFieldCount.Should().Be(
            report.Pages.Sum(p => p.ImportableFieldCount) + report.UnplacedImportableFieldCount,
            "a field that belongs to no page (a non-terminal field-tree node) still counts " +
            "toward the document, so the totals only reconcile once those are accounted for");
    }

    [Fact]
    public void SourcesCompose_RatherThanExcludingEachOther()
    {
        var both = PdfSourceDetector.Detect(new FillablePdfDocumentRenderer().RenderToBytes(SourceForm()));
        var textOnly = PdfSourceDetector.Detect(new PdfDocumentRenderer().RenderToBytes(SourceForm()));
        var fieldsOnly = PdfSourceDetector.Detect(ScannedCorpusForm());

        // The point of a flags enum here: "has an AcroForm" and "has text" are
        // independent facts, and the interesting case is holding both at once.
        both.Sources.Should().Be(PdfFieldSources.AcroForm | PdfFieldSources.TextLayer);
        textOnly.Sources.Should().Be(PdfFieldSources.TextLayer);
        fieldsOnly.Sources.Should().Be(PdfFieldSources.AcroForm);

        // Three of the four combinations, from real and rendered documents. The
        // fourth -- None, a scan with no widgets over it -- has no fixture: every
        // one of the 11 corpus forms turned out to carry an AcroForm. Rather than
        // assert it against a document that does not exist, this is left uncovered
        // and recorded as a corpus gap.
    }

    /// <summary>
    /// The one form in-repo whose printed content is pixels rather than text (it
    /// still carries AcroForm widgets on top). Read from the benchmark
    /// corpus rather than copied into <c>tests/Fixtures</c>, because that folder's
    /// README states source PDFs are deliberately not committed there.
    /// </summary>
    private static byte[] ScannedCorpusForm()
    {
        var path = Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "corpus", "ct-dmv-a25.pdf");
        File.Exists(path).Should().BeTrue(
            $"the scanned fixture is committed at {path}; without it this suite would " +
            "silently stop covering the no-text-layer case, which is the one a binary " +
            "scanned/not-scanned check gets wrong");
        return File.ReadAllBytes(path);
    }

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
}
