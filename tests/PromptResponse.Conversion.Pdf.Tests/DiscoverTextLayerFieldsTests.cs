using AwesomeAssertions;
using PromptResponse.Conversion.Pdf;
using PromptResponse.Rendering.Pdf;
using Xunit;

namespace PromptResponse.Conversion.Pdf.Tests;

/// <summary>
/// The converter's actual target: a form with no AcroForm, where the blank a
/// person writes on exists only as ink. Graded against the fillable source's
/// widget rectangles, which is ground truth needing no human and no model.
/// </summary>
public class DiscoverTextLayerFieldsTests
{
    [Fact]
    public void EveryTickBoxOnTheFlatW9IsFound()
    {
        // The ceiling test. A tick box is drawn as a small square and there is
        // nothing to infer, so anything less than all of them means the reader
        // is broken rather than the heuristics being imperfect.
        var placed = Discover("fed-w9-flat").Where(p => p.Rect.Right - p.Rect.Left <= 14).ToList();
        var boxes = Answer("fed-w9").Importable
            .Where(e => e.FieldType == Excise.Core.Document.PdfFieldType.Button && e.Rect is not null)
            .ToList();

        boxes.Should().HaveCount(8, "W-9 declares eight tick boxes");

        var coverage = PdfGeometryCoverage.Compare(
            new WidgetManifest([.. boxes]), placed);

        coverage.Covered.Should().Be(8,
            $"every tick box is a drawn square; missing {string.Join(", ", coverage.MissingFieldNames)}");
    }

    [Fact]
    public void CharacterCombsAreReassembledIntoWholeFields()
    {
        // W-9 draws its SSN and EIN as one box per character: the 43.2pt SSN
        // field is three 14.4pt cells, not three fields. Emitting the cells
        // would triple the field count and place none of them, so the cells
        // must be merged back into the field the form is asking for.
        //
        // The tolerance matters and is not obvious: the gap between SSN groups
        // is exactly one cell wide, so a "within a cell" merge tolerance would
        // swallow the whole row into a single field.
        var combs = Answer("fed-w9").Importable
            .Where(e => e.Rect is { } r && Math.Abs(r.Top - r.Bottom - 24) < 0.5)
            .ToList();
        combs.Should().HaveCount(5, "three SSN groups and two EIN groups");

        var coverage = PdfGeometryCoverage.Compare(new WidgetManifest([.. combs]), Discover("fed-w9-flat"));

        coverage.Covered.Should().Be(5,
            $"each comb group is one field; missing {string.Join(", ", coverage.MissingFieldNames)}");
    }

    // Floors set just under the measured values, so a regression fails and an
    // improvement does not need the test edited. Measured: W-9 100.0% coverage
    // with 20 invented, I-9 98.4% with 109.
    [Theory]
    [InlineData("fed-w9-flat", "fed-w9", 0.95, 22)]
    [InlineData("fed-i9-flat", "fed-i9", 0.95, 115)]
    public void TheFlatFormsFieldsAreFoundWhereTheSourceDeclaresThem(
        string flat, string source, double minimumFraction, int maximumInvented)
    {
        var coverage = PdfGeometryCoverage.Compare(Answer(source), Discover(flat));

        coverage.Fraction.Should().BeGreaterThanOrEqualTo(minimumFraction,
            $"{flat}: found {coverage.Covered} of {coverage.Expected}. " +
            $"Missing: {string.Join(", ", coverage.MissingFieldNames.Take(8))}");

        // Coverage alone is gameable -- a converter raises it by guessing more.
        // The precision half is what stops that, so both are asserted together.
        coverage.UnaccountedPromptIds.Count.Should().BeLessThanOrEqualTo(maximumInvented,
            $"{flat}: {coverage.UnaccountedPromptIds.Count} emitted rects matched no field. " +
            "Every drawn line is not a blank, and a reader that says so is not useful");
    }

    private static IReadOnlyList<PlacedPrompt> Discover(string id)
    {
        var state = ConversionPipeline.Default().Convert(CorpusPath(id), id).State;
        return [.. state.FieldsOrEmpty
            .Where(f => f.TargetRect is not null)
            .Select(f => new PlacedPrompt(f.Id, f.PageNumber, f.TargetRect!.Value))];
    }

    private static WidgetManifest Answer(string id) => PdfWidgetManifest.Extract(CorpusPath(id));

    private static string CorpusPath(string id) =>
        Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "corpus", $"{id}.pdf");

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
}
