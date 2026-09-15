using AwesomeAssertions;
using PromptResponse.Conversion.Pdf;
using PromptResponse.Rendering.Pdf;
using Xunit;

namespace PromptResponse.Conversion.Pdf.Tests;

/// <summary>
/// Where a correct label dies. Each phase can discard the right answer, and a
/// phase-level score cannot say which one did.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline is five gates a label must pass: it has to be extracted, survive
/// being merged into a block, survive classification, be close enough to the
/// field for pairing to consider it, and then win against every other candidate.
/// A correct answer discarded at gate two is invisible to every measurement
/// taken after it, which is how "the classifier is the problem" stayed true long
/// after it stopped being true.
/// </para>
/// <para>
/// Measured on fed-i9's 128 tooltip-graded fields, this is what moved: before
/// the sentence-threshold fix and the rescue pass, classification discarded 13
/// correct labels. It now discards 2, and the loss has moved to reachability —
/// the right text survives every filter and is simply too far from its field for
/// the distance limits to consider it.
/// </para>
/// </remarks>
public class LabelSurvivalTests
{
    private sealed record Funnel(int Total, int Extracted, int Blocked, int Classified, int Reachable, int Chosen);

    [Fact]
    public void ExtractionAndClassificationAreNoLongerWhereLabelsDie()
    {
        // The early phases must stay nearly lossless. If either starts dropping
        // correct answers again, no later tuning can recover them, and the
        // end-to-end score will fall for a reason no label metric can localise.
        var f = Measure("fed-i9");

        f.Extracted.Should().BeGreaterThanOrEqualTo((int)(f.Total * 0.98),
            "the text layer contains the questions; failing to read them is unrecoverable");
        f.Blocked.Should().BeGreaterThanOrEqualTo(f.Extracted - 3,
            "merging lines into blocks must not destroy the label it is assembling");
        f.Classified.Should().BeGreaterThanOrEqualTo(f.Blocked - 5,
            "ruling text out is the cheapest place to lose a correct answer and the " +
            "hardest to notice: it was discarding 13 before the sentence threshold " +
            "was measured rather than guessed");
    }

    [Fact]
    public void TheBindingConstraintIsReachability_NotFiltering()
    {
        // Records which gate is currently costing the most, so work goes there.
        // If this ever fails it means the ordering changed and the next fix
        // belongs somewhere else -- which is the point of asserting it.
        var f = Measure("fed-i9");

        var lostToFiltering = (f.Total - f.Extracted) + (f.Extracted - f.Blocked) + (f.Blocked - f.Classified);
        var lostToReach = f.Classified - f.Reachable;

        lostToReach.Should().BeGreaterThan(lostToFiltering,
            $"the correct label now survives every filter and is out of range instead: " +
            $"filtering loses {lostToFiltering}, distance limits lose {lostToReach}");
    }

    [Fact]
    public void WinningTheCompetitionIsNotWhereLabelsAreLost()
    {
        // The greedy one-span-one-field rule was suspected of starving fields.
        // It costs 2 of 128 on I-9, so it is not worth relaxing -- and relaxing
        // it was measured and deleted once already.
        var f = Measure("fed-i9");

        (f.Reachable - f.Chosen).Should().BeLessThan(5,
            "a reachable correct label almost always wins; competition is not the problem");
    }

    private static Funnel Measure(string id)
    {
        var man = PdfWidgetManifest.Extract(CorpusPath(id));
        var expected = man.Importable
            .Where(e => e.HasTooltip && !ImportQualityHeuristics.LooksCryptic(e.Tooltip!))
            .ToDictionary(e => e.FullName, e => e.Tooltip!);

        var run = ConversionPipeline.WithoutFormAuthorLabels().Convert(CorpusPath(id), id);
        var fields = run.State.FieldsOrEmpty.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First());
        var boilerplate = LabelAgreement.Boilerplate(expected.Values);

        bool Right(string? text, string want) =>
            LabelAgreement.Score(text, want, boilerplate) >= LabelAgreement.AgreementThreshold;

        int total = 0, extracted = 0, blocked = 0, classified = 0, reachable = 0, chosen = 0;
        foreach (var (name, want) in expected)
        {
            if (!fields.TryGetValue(name, out var field) || field.TargetRect is null) continue;
            var rect = field.TargetRect.Value;
            total++;

            if (!run.State.MeasuredOrEmpty.Any(s => s.PageNumber == field.PageNumber && Right(s.Text, want))) continue;
            extracted++;

            var candidates = run.State.SpansOrEmpty
                .Where(s => s.PageNumber == field.PageNumber && Right(s.Text, want))
                .ToList();
            if (candidates.Count == 0) continue;
            blocked++;

            // Label means recovery chose it, so it survived classification too.
            var alive = candidates
                .Where(s => s.Role is SpanRole.Unclassified or SpanRole.Label
                    || (s.Role is SpanRole.Heading or SpanRole.Instruction
                        && s.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= LabelRecovery.RescueMaximumWords))
                .ToList();
            if (alive.Count == 0) continue;
            classified++;

            var directions = LabelRecovery.DirectionsFor(field, rect);
            if (!alive.Any(s => directions.Any(d => LabelRecovery.Distance(rect, s.Rect, d) is not null))) continue;
            reachable++;

            if (Right(field.Label, want)) chosen++;
        }

        return new Funnel(total, extracted, blocked, classified, reachable, chosen);
    }

    private static string CorpusPath(string id) =>
        Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "corpus", $"{id}.pdf");

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
}
