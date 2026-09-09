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
        var neither = PdfSourceDetector.Detect(CorpusForm("ct-dmv-a25-flat"));

        // The point of a flags enum here: "has an AcroForm" and "has text" are
        // independent facts, and the interesting case is holding both at once.
        both.Sources.Should().Be(PdfFieldSources.AcroForm | PdfFieldSources.TextLayer);
        textOnly.Sources.Should().Be(PdfFieldSources.TextLayer);
        fieldsOnly.Sources.Should().Be(PdfFieldSources.AcroForm);

        // The fourth combination, which went uncovered while every corpus form
        // turned out to carry an AcroForm. It is the same A-25 scan as
        // `fieldsOnly` with its widgets removed, so the pair isolates exactly one
        // variable: same pixels, same absent text, AcroForm or not.
        neither.Sources.Should().Be(PdfFieldSources.None);
        neither.IsImageOnly.Should().BeTrue(
            "pixels are all there is, so this is the one case that genuinely needs OCR");
    }

    [Fact]
    public void TheDerivedFixturesAreWhatTheManifestClaims()
    {
        // Fixtures synthesized by scripts/pdf-form-benchmark/synthesize_fixtures.py.
        // They exist because agencies have almost entirely moved to fillable PDFs:
        // all five federal corpus forms carry an AcroForm, so without these there
        // is no federal non-AcroForm case and no image-only case at all.
        //
        // Asserting the claim rather than trusting it matters here, because the
        // flattener is a third-party tool: if a poppler upgrade started preserving
        // widgets, or dropped the text layer, these fixtures would quietly stop
        // testing what they were built to test.
        foreach (var id in new[] { "fed-w9-flat", "fed-ss4-flat", "fed-8822-flat", "ct-w4-flat", "ct-dmv-j23-flat" })
        {
            var report = PdfSourceDetector.Detect(CorpusForm(id));
            report.HasAcroForm.Should().BeFalse($"{id} was printed to a flat PDF, which drops the widgets");
            report.HasTextLayer.Should().BeTrue($"{id} was printed, not scanned, so its text survives exactly");
            report.Sources.Should().Be(PdfFieldSources.TextLayer);
        }

        foreach (var id in new[] { "ct-dmv-a25-flat", "fed-8822-scan" })
        {
            PdfSourceDetector.Detect(CorpusForm(id)).IsImageOnly.Should().BeTrue(
                $"{id} is an image-only page and must stay one");
        }
    }

    [Fact]
    public void AFlattenedFormKeepsEveryWordOfItsSource()
    {
        // The reason a flattened fixture is a fair test of the converter's
        // text-layer path and not a degraded one: printing to PDF re-encodes the
        // page, and a flattener that subtly dropped or reordered text would make
        // the fixture easier or harder than the form it came from, invisibly.
        var source = PdfSourceDetector.Detect(CorpusForm("fed-w9"));
        var flat = PdfSourceDetector.Detect(CorpusForm("fed-w9-flat"));

        flat.CharacterCount.Should().Be(source.CharacterCount,
            "flattening removes the widgets, not the words");
        flat.Pages.Should().HaveCount(source.Pages.Count);
    }

    /// <summary>
    /// The one form in-repo whose printed content is pixels rather than text (it
    /// still carries AcroForm widgets on top). Read from the benchmark
    /// corpus rather than copied into <c>tests/Fixtures</c>, because that folder's
    /// README states source PDFs are deliberately not committed there.
    /// </summary>
    private static byte[] ScannedCorpusForm() => CorpusForm("ct-dmv-a25");

    /// <summary>
    /// Reads a form from the benchmark corpus rather than <c>tests/Fixtures</c>,
    /// because that folder's README states source PDFs are deliberately not
    /// committed there.
    /// </summary>
    private static byte[] CorpusForm(string id)
    {
        var path = Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "corpus", $"{id}.pdf");
        File.Exists(path).Should().BeTrue(
            $"{id} is a committed corpus form; without it this suite would silently " +
            "stop covering whichever source combination it stands for");
        return File.ReadAllBytes(path);
    }

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
}
