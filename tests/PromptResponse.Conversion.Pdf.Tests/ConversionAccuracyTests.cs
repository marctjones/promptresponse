using AwesomeAssertions;
using PromptResponse.Conversion.Pdf;
using PromptResponse.Rendering.Pdf;
using Xunit;

namespace PromptResponse.Conversion.Pdf.Tests;

/// <summary>
/// The end-to-end number: what fraction of a form's real fields come out both
/// found and correctly labelled.
/// </summary>
/// <remarks>
/// <para>
/// Placement and labelling each have their own gate, and each can look healthy
/// while the conversion is not. A field found at the right place with the wrong
/// question is not a usable field, and neither is a perfect label attached to
/// something that is not a field. <b>Usable</b> counts only fields that are
/// both, over every field the form really has.
/// </para>
/// <para>
/// It cannot be gamed by guessing more, which is the failure mode
/// <see cref="PdfGeometryCoverage"/> warns about: the numerator counts real
/// fields, so emitting extra candidates cannot raise it. It is still reported
/// beside precision, because a converter could reach a decent usable score
/// while burying it in noise, and noise is what the model phase has to pay for.
/// </para>
/// </remarks>
public class ConversionAccuracyTests
{
    [Fact]
    public void MostOfTheFlatI9sFieldsAreFoundAndCorrectlyLabelled()
    {
        var score = Measure("fed-i9-flat", "fed-i9");

        // Floors just under measured. Placement is close to solved; labelling
        // is not, and the gap between these two numbers is the whole point of
        // measuring end to end rather than per phase.
        score.Placement.Fraction.Should().BeGreaterThanOrEqualTo(0.95,
            "the blanks are drawn on the page and geometry finds them");
        score.Usable.Should().BeGreaterThanOrEqualTo(0.35,
            $"only {score.CorrectlyLabelled} of {score.RealFields} fields are both found and " +
            "correctly labelled; labelling is the binding constraint, not discovery");
    }

    [Fact]
    public void FindingAFieldIsNotTheSameAsConvertingIt()
    {
        // Guards the metric against the reading that would make it useless.
        // If Usable ever equals placement recall, the label half has stopped
        // discriminating and this test is measuring one thing twice.
        var score = Measure("fed-i9-flat", "fed-i9");

        score.Usable.Should().BeLessThan(score.Placement.Fraction,
            "a found field is not yet a converted one, and a metric that cannot " +
            "tell those apart is not measuring the conversion");
    }

    [Fact]
    public void DiscoveryFavoursFindingEverythingOverGuessingPrecisely()
    {
        // Records the deliberate bias, so that flipping it is a decision rather
        // than a drift. A missed field is terminal: no later phase re-reads the
        // page, so nothing downstream can recover it. A spurious field is
        // prunable. Discovery is therefore scored with recall weighted heavily.
        var placement = Measure("fed-w9-flat", "fed-w9").Placement;

        placement.FScore(2).Should().BeGreaterThan(placement.FScore(1),
            "recall exceeds precision here by design; if that reverses, the " +
            "detector has started missing fields to look tidy");
        placement.Fraction.Should().BeGreaterThan(placement.Precision);
    }

    private static (WidgetCoverage Placement, double Usable, int CorrectlyLabelled, int RealFields)
        Measure(string flat, string source)
    {
        var root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
        string Corpus(string id) => Path.Combine(root, "scripts", "pdf-form-benchmark", "corpus", $"{id}.pdf");

        var fields = ConversionPipeline.Default().Convert(Corpus(flat), flat)
            .State.FieldsOrEmpty.Where(f => f.TargetRect is not null).ToList();
        var manifest = PdfWidgetManifest.Extract(Corpus(source));

        var placement = PdfGeometryCoverage.Compare(manifest,
            [.. fields.Select(f => new PlacedPrompt(f.Id, f.PageNumber, f.TargetRect!.Value))]);

        var expected = manifest.Importable
            .Where(e => e.HasTooltip && !ImportQualityHeuristics.LooksCryptic(e.Tooltip!))
            .ToDictionary(e => e.FullName, e => e.Tooltip!);
        var recovered = fields.ToDictionary(f => f.Id, f => f.Label);
        var boilerplate = LabelAgreement.Boilerplate(expected.Values);

        var right = placement.MatchesOrEmpty
            .Where(m => expected.ContainsKey(m.FieldName))
            .Count(m => LabelAgreement.Score(
                recovered.GetValueOrDefault(m.PromptId), expected[m.FieldName], boilerplate)
                    >= LabelAgreement.AgreementThreshold);

        return (placement, expected.Count == 0 ? 0 : (double)right / expected.Count, right, expected.Count);
    }
}
