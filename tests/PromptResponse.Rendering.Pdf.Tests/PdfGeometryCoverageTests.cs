using AwesomeAssertions;
using Excise.Core.Document;
using Xunit;
using PdfeDoc = Excise.Core.Document.PdfDocument;

namespace PromptResponse.Rendering.Pdf.Tests;

/// <summary>
/// The oracle that turns the derived fixtures' answer keys. Matching on where a
/// field is, rather than what it is called, is the only way to grade a converter
/// that never saw the source PDF's field names.
/// </summary>
public class PdfGeometryCoverageTests
{
    [Fact]
    public void AConversionThatFoundEveryBlankScoresComplete()
    {
        // The source's own widgets, offered back as if a converter had located
        // them perfectly. This is the ceiling: if an exact answer does not score
        // 100%, nothing else the oracle says can be trusted.
        var manifest = PdfWidgetManifest.Extract(CorpusPath("fed-w9"));
        var perfect = AsPlacedPrompts(manifest);

        var coverage = PdfGeometryCoverage.Compare(manifest, perfect);

        coverage.IsComplete.Should().BeTrue(
            $"missing: {string.Join(", ", coverage.MissingFieldNames)}");
        coverage.Fraction.Should().Be(1.0);
        coverage.UnaccountedPromptIds.Should().BeEmpty();
        coverage.Expected.Should().Be(23);
    }

    [Fact]
    public void CoverageAloneIsNotAScore_TheChanceFloorIsReal()
    {
        // The control that stops this being a test of nothing -- and it found
        // something worth knowing rather than simply passing.
        //
        // fed-ss4's field positions, offered as a conversion of fed-w9, cover
        // 35% of W-9's fields. Not because the oracle is broken: government
        // forms share a page size and standard line spacing, so a dense form
        // sprays candidates across positions another form also uses. Measured
        // across five corpus forms the cross-form floor runs 0-48%, and it
        // tracks how MANY fields the wrong form has, not how similar it is:
        // fed-ss4 (89 fields) scores highest against everything.
        //
        // The consequence is the point: a converter can raise coverage simply
        // by emitting more guesses. Coverage must therefore always be read with
        // UnaccountedPromptIds -- recall paired with precision -- and a bare
        // "found 60% of fields" means nothing on its own.
        var w9 = PdfWidgetManifest.Extract(CorpusPath("fed-w9"));
        var wrongForm = AsPlacedPrompts(PdfWidgetManifest.Extract(CorpusPath("fed-ss4")));

        var coverage = PdfGeometryCoverage.Compare(w9, wrongForm);

        coverage.Fraction.Should().BeLessThan(0.5,
            "a different form must not pass as this one, even by chance");
        coverage.Fraction.Should().BeGreaterThan(0,
            "and pretending the chance floor is zero would be the more dangerous error");

        coverage.UnaccountedPromptIds.Count.Should().BeGreaterThan(coverage.Covered * 2,
            "the noise is visible in the precision half: most of what the wrong form " +
            "offered matched nothing at all, which is exactly what a caller reading " +
            "only the coverage fraction would miss");
    }

    [Fact]
    public void TwoPromptsOnOneBlankCannotSatisfyTwoWidgets()
    {
        // Without consuming a match, a converter that dumped many prompts onto
        // one location would score well by covering the same widget repeatedly.
        var manifest = PdfWidgetManifest.Extract(CorpusPath("fed-w9"));
        var oneBlank = manifest.Importable.First(e => e.Rect is not null);

        var piledUp = Enumerable.Range(1, 10)
            .Select(i => new PlacedPrompt($"guess-{i}", oneBlank.PageNumber!.Value, oneBlank.Rect!.Value))
            .ToList();

        var coverage = PdfGeometryCoverage.Compare(manifest, piledUp);

        coverage.Covered.Should().Be(1, "ten prompts on one blank still find one field");
        coverage.UnaccountedPromptIds.Should().HaveCount(9,
            "the other nine matched nothing and should be reported as invented");
    }

    [Fact]
    public void AThinDrawnRuleInsideAWidgetIsAMatch()
    {
        // The case that decided the metric, and the one a real converter will
        // actually produce. A form's write-on blank is a rule roughly 1pt tall
        // sitting on the baseline of a widget 8-38pt tall, so the two rectangles
        // describing one field differ in area by an order of magnitude even when
        // the answer is exactly right.
        var manifest = PdfWidgetManifest.Extract(CorpusPath("fed-w9"));

        var rules = manifest.Importable
            .Where(e => e.Rect is not null)
            .Select((e, i) => new PlacedPrompt($"rule-{i}", e.PageNumber!.Value,
                new PdfRectangle(e.Rect!.Value.Left, e.Rect.Value.Bottom, e.Rect.Value.Right, e.Rect.Value.Bottom + 1)))
            .ToList();

        PdfGeometryCoverage.Compare(manifest, rules).IsComplete.Should().BeTrue(
            "a rule drawn on the widget's baseline is the same field");

        // And the measurement behind that choice: intersection-over-union scores
        // this ideal answer at 0.07 and would throw it away.
        var widget = manifest.Importable.First(e => e.Rect is not null).Rect!.Value;
        var rule = new PdfRectangle(widget.Left, widget.Bottom, widget.Right, widget.Bottom + 1);
        PdfGeometryCoverage.IntersectionOverUnion(widget, rule).Should().BeLessThan(0.1);
        PdfGeometryCoverage.Overlap(widget, rule).Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void OneBoxPerPageDoesNotPassAsFindingEveryField()
    {
        // The failure mode overlap-over-minimum invites: a rectangle that merely
        // contains a widget scores 1.0 against it. Without the centre check, a
        // converter that emitted a single page-sized box would score a perfect
        // result on every field of that page.
        var manifest = PdfWidgetManifest.Extract(CorpusPath("fed-w9"));
        var wholePage = manifest.Importable
            .Where(e => e.PageNumber is not null)
            .Select(e => e.PageNumber!.Value)
            .Distinct()
            .Select(page => new PlacedPrompt($"page-{page}", page, new PdfRectangle(0, 0, 612, 792)))
            .ToList();

        var coverage = PdfGeometryCoverage.Compare(manifest, wholePage);

        coverage.Covered.Should().Be(0, "covering a field is not finding it");
        coverage.MissingFieldNames.Should().HaveCount(coverage.Expected);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [InlineData(12, false)]
    [InlineData(40, false)]
    public void MatchingToleratesSmallDisplacementAndRejectsLarge(double shift, bool expectedToMatch)
    {
        // Characterises the tolerance rather than asserting a number from
        // nowhere. fed-w9's widgets are 8-38pt tall (median 14), so a 12pt
        // displacement lands on the next line down -- a different field.
        var manifest = PdfWidgetManifest.Extract(CorpusPath("fed-w9"));
        var displaced = AsPlacedPrompts(manifest)
            .Select(p => p with { Rect = new PdfRectangle(p.Rect.Left, p.Rect.Bottom - shift, p.Rect.Right, p.Rect.Top - shift) })
            .ToList();

        var coverage = PdfGeometryCoverage.Compare(manifest, displaced);

        if (expectedToMatch)
        {
            coverage.Fraction.Should().BeGreaterThan(0.9, $"a {shift}pt displacement is within drawing tolerance");
        }
        else
        {
            coverage.Fraction.Should().BeLessThan(0.5, $"a {shift}pt displacement is a different field");
        }
    }

    [Fact]
    public void TheOffsetHandlesTheOneFixtureWhoseOriginMoved()
    {
        // ct-dmv-a25-flat is the single derived fixture whose page origin shifted:
        // flattening baked in its source's CropBox of [0 198 612 585], so a
        // converter reading it reports coordinates 198 points lower than the
        // source's widget rects. Without the offset the answer key silently
        // grades every field wrong -- which is worse than having no key, because
        // it looks like a converter bug.
        var manifest = PdfWidgetManifest.Extract(CorpusPath("ct-dmv-a25"));
        const double cropBoxBottom = 198;

        var asSeenInTheFlatFixture = AsPlacedPrompts(manifest)
            .Select(p => p with { Rect = new PdfRectangle(p.Rect.Left, p.Rect.Bottom - cropBoxBottom, p.Rect.Right, p.Rect.Top - cropBoxBottom) })
            .ToList();

        PdfGeometryCoverage.Compare(manifest, asSeenInTheFlatFixture, offsetY: -cropBoxBottom)
            .IsComplete.Should().BeTrue("the documented offset is what makes this fixture's key usable");

        PdfGeometryCoverage.Compare(manifest, asSeenInTheFlatFixture)
            .Fraction.Should().BeLessThan(0.5,
                "and without it the same correct conversion looks broken, which is why " +
                "the offset is recorded in the corpus manifest rather than left to be rediscovered");
    }

    [Fact]
    public void UnplaceableFieldsAreNotCountedAgainstAConverter()
    {
        // A non-terminal field-tree node (§12.7.3.2) has a name and children but
        // no widget, so there is nothing on the page to find. Counting it as
        // missing would penalise a converter for something invisible to it.
        var manifest = PdfWidgetManifest.Extract(CorpusPath("fed-w9"));
        var placeable = manifest.Importable.Count(e => e.Rect is not null && e.PageNumber is not null);

        PdfGeometryCoverage.Compare(manifest, AsPlacedPrompts(manifest))
            .Expected.Should().Be(placeable);
    }

    [Fact]
    public void OverlapIsSymmetricAndBounded()
    {
        var a = new PdfRectangle(0, 0, 10, 10);
        var b = new PdfRectangle(5, 0, 15, 10);

        PdfGeometryCoverage.IntersectionOverUnion(a, a).Should().Be(1.0);
        PdfGeometryCoverage.IntersectionOverUnion(a, b)
            .Should().BeApproximately(PdfGeometryCoverage.IntersectionOverUnion(b, a), 1e-9);
        PdfGeometryCoverage.IntersectionOverUnion(a, new PdfRectangle(100, 100, 110, 110))
            .Should().Be(0, "disjoint rectangles overlap not at all");
    }

    private static List<PlacedPrompt> AsPlacedPrompts(WidgetManifest manifest) =>
        [.. manifest.Importable
            .Where(e => e.Rect is not null && e.PageNumber is not null)
            .Select((e, i) => new PlacedPrompt($"converted-{i}", e.PageNumber!.Value, e.Rect!.Value))];

    private static string CorpusPath(string id) =>
        Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "corpus", $"{id}.pdf");

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
}
