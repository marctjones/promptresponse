using AwesomeAssertions;
using PromptResponse.Conversion.Pdf;
using PromptResponse.Rendering.Pdf;
using Xunit;

namespace PromptResponse.Conversion.Pdf.Tests;

/// <summary>
/// The only mechanical oracle for whether a recovered label is the RIGHT label.
/// A form author's <c>/TU</c> text is their own words for the field, so holding
/// it back and grading recovery against it needs no human and no model.
/// </summary>
public class LabelAgreementTests
{
    /// <summary>Forms whose authors wrote real tooltips. The rest of the corpus has none.</summary>
    public static TheoryData<string, double, double> Gradable() => new()
    {
        // form, minimum recall, minimum precision -- floors, not targets.
        { "fed-i9", 0.65, 0.82 },
        { "ct-w4", 0.45, 0.70 },
    };

    [Theory]
    [MemberData(nameof(Gradable))]
    public void RecoveredLabelsAgreeWithTheFormAuthorsOwnWords(string id, double minRecall, double minPrecision)
    {
        var result = Grade(id);

        result.Recall.Should().BeGreaterThanOrEqualTo(minRecall,
            $"{id}: agreed {result.Agreed} of {result.Gradable} gradable fields. " +
            $"Worst misses: {WorstMisses(result)}");
        result.Precision.Should().BeGreaterThanOrEqualTo(minPrecision,
            $"{id}: of {result.Attempted} labels recovery produced, {result.Agreed} match the author's");
    }

    [Theory]
    [MemberData(nameof(Gradable))]
    public void TheMetricCanTellAFieldsLabelFromItsNeighbours(string id, double minRecall, double minPrecision)
    {
        // The control that stops this being a gate that measures nothing. If
        // scoring each field against the NEXT field's label did as well as
        // scoring it against its own, the metric would be matching boilerplate.
        _ = minRecall;
        _ = minPrecision;
        var result = Grade(id);

        result.ChanceFloor.Should().BeLessThan(result.Recall,
            $"{id}: shifting the answer key by one field scores {result.ChanceFloor:P0} against " +
            $"the real {result.Recall:P0}. A metric that cannot tell them apart is measuring " +
            "the form's boilerplate, not whether the right question was found");
    }

    [Fact]
    public void HoldingBackTheAuthorsLabelsIsWhatMakesTheGradeMeanAnything()
    {
        // Guards the oracle itself. With tooltips honoured, fed-i9 scores 128/128
        // while exercising no label recovery at all -- every field short-circuits
        // on the author's text. That number looks like success and grades nothing,
        // so the suppressed pipeline must actually differ from the default one.
        var path = CorpusPath("fed-i9");

        var honoured = ConversionPipeline.Default().Convert(path, "fed-i9")
            .State.FieldsOrEmpty.Count(f => f.LabelSource == LabelSource.FormAuthor);
        var suppressed = ConversionPipeline.WithoutFormAuthorLabels().Convert(path, "fed-i9")
            .State.FieldsOrEmpty.Count(f => f.LabelSource == LabelSource.FormAuthor);

        honoured.Should().BeGreaterThan(100, "I-9's author labelled every field");
        suppressed.Should().Be(0, "and the graded run must not be allowed to see any of them");
    }

    private static FormLabelAgreement Grade(string id)
    {
        var path = CorpusPath(id);
        var expected = PdfWidgetManifest.Extract(path).Importable
            .Where(e => e.HasTooltip && !ImportQualityHeuristics.LooksCryptic(e.Tooltip!))
            .Select(e => (e.FullName, e.Tooltip!))
            .ToList();

        var recovered = ConversionPipeline.WithoutFormAuthorLabels().Convert(path, id)
            .State.FieldsOrEmpty
            .GroupBy(f => f.Id)
            .ToDictionary(g => g.Key, g => g.First().Label);

        return LabelAgreement.Compare(id, expected, recovered);
    }

    private static string WorstMisses(FormLabelAgreement result) =>
        string.Join(" | ", result.Matches
            .Where(m => !m.IsAgreement)
            .Take(5)
            .Select(m => $"got '{m.Recovered ?? "(none)"}' want '{m.Expected}'"));

    private static string CorpusPath(string id) =>
        Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "corpus", $"{id}.pdf");

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
}
