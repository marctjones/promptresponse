using AwesomeAssertions;
using PromptResponse.Conversion.Pdf;
using PromptResponse.Rendering.Pdf;
using Xunit;

namespace PromptResponse.Conversion.Pdf.Tests;

/// <summary>
/// The pipeline's job today is to route correctly and to be honest about what it
/// cannot do. Most phases are not built, so these tests are mostly about the
/// second of those — a pipeline that looks like it ran eight steps when it ran
/// three is the more expensive kind of wrong.
/// </summary>
public class ConversionPipelineTests
{
    [Fact]
    public void EveryPhaseReportsSomething_EvenWhenItDoesNothing()
    {
        var result = ConversionPipeline.Default().Convert(CorpusPath("fed-w9"), "fed-w9");

        result.Phases.Should().HaveCount(8);
        result.Phases.Select(p => p.Name).Should().Equal(
            "detect-sources", "extract-text", "discover-acroform", "discover-text-layer",
            "classify-spans", "recover-labels", "assemble", "model-touch-up");
        result.Phases.Should().AllSatisfy(p => p.Detail.Should().NotBeNullOrWhiteSpace(
            "a phase that reports nothing cannot be diagnosed"));
    }

    [Fact]
    public void AFillableFormIsConverted_AndLosesNoField()
    {
        var result = ConversionPipeline.Default().Convert(CorpusPath("fed-w9"), "fed-w9");

        result.Succeeded.Should().BeTrue();
        var coverage = PdfWidgetManifest.Compare(
            PdfWidgetManifest.Extract(CorpusPath("fed-w9")), result.State.Document!);

        coverage.IsComplete.Should().BeTrue(
            $"missing: {string.Join(", ", coverage.MissingFieldNames)}");
        coverage.UnaccountedPromptIds.Should().BeEmpty("the pipeline must invent nothing");
    }

    [Fact]
    public void AFlatFormNowYieldsFieldsFromWhatThePageDraws()
    {
        // The converter's actual target case. This test previously asserted the
        // opposite -- that a flat form produced nothing and said so -- and was
        // written to fail loudly when #424 landed. It has.
        var result = ConversionPipeline.Default().Convert(CorpusPath("fed-w9-flat"), "flat");

        result.Succeeded.Should().BeTrue("the blanks are drawn on the page even with no AcroForm");
        result.Unbuilt.Select(p => p.Name).Should().NotContain("discover-text-layer");

        result.Phases.Single(p => p.Name == "discover-acroform").Status.Should().Be(PhaseStatus.Skipped,
            "there is no AcroForm to read; every field here came from the page's own ink");
        result.State.FieldsOrEmpty.Should().OnlyContain(f => f.Origin == FieldOrigin.TextLayer);
        result.State.FieldsOrEmpty.Should().OnlyContain(f => f.TargetRect != null,
            "a field found by geometry always knows where it is, which is what makes it gradable");
    }

    [Fact]
    public void AnImageOnlyScanSkipsTextExtractionRatherThanReturningEmptyText()
    {
        var result = ConversionPipeline.Default().Convert(CorpusPath("ct-dmv-a25-flat"), "scan");

        result.Phases.Single(p => p.Name == "extract-text").Status.Should().Be(PhaseStatus.Skipped);
        result.State.SpansOrEmpty.Should().BeEmpty();
    }

    [Fact]
    public void AnEmptyReviewQueueIsNotReportedAsSuccessWhenNothingWasFound()
    {
        // "No fields need review" and "no fields were found" produce the same
        // empty queue. Reporting the second as the first is a phase claiming
        // credit for work that never happened, and it is the exact shape of a
        // green check that measures nothing.
        var found = ConversionPipeline.Default().Convert(CorpusPath("fed-w9"), "w9");
        var nothing = ConversionPipeline.Default().Convert(CorpusPath("ct-dmv-a25-flat"), "scan");

        found.ReviewQueueSize.Should().BeGreaterThan(0, "W-9's fields are all cryptic");
        nothing.ReviewQueueSize.Should().Be(0);
        nothing.Phases.Single(p => p.Name == "model-touch-up").Detail
            .Should().Contain("found nothing",
                "the report must distinguish the two, since the number alone cannot");
    }

    [Fact]
    public void CrypticTooltipsAreNotAcceptedAsLabels()
    {
        // ct-dmv-a25 carries a /TU on every field and every one of them says
        // "TextField1". Treating a present tooltip as a real label would report
        // this form as fully labelled and remove it from the work queue.
        var result = ConversionPipeline.Default().Convert(CorpusPath("ct-dmv-a25"), "a25");

        result.State.FieldsOrEmpty.Should().NotBeEmpty();
        result.State.FieldsOrEmpty.Should().AllSatisfy(f => f.HasLabel.Should().BeFalse(
            "every tooltip on this form is the string 'TextField1'"));
        result.ReviewQueueSize.Should().Be(result.State.FieldsOrEmpty.Count);
    }

    [Fact]
    public void TheDuplicatedCrypticHeuristicMatchesTheOneItCopied()
    {
        // ImportQualityHeuristics duplicates a private helper in
        // PdfImportQualityAssessor because the converter is being built
        // segregated from that assembly. A duplicate nobody checks is a
        // duplicate that drifts, so this compares the two across the corpus via
        // the public CrypticLabelRatio.
        string[] forms =
        [
            "fed-w9", "fed-w4", "fed-ss4", "fed-8822", "fed-i9",
            "ct-w4", "ct-dmv-j23", "ct-dmv-b58ind", "ct-dmv-a25", "ct-dmv-a83", "ct-dmv-b225p",
        ];

        foreach (var id in forms)
        {
            var (document, quality) = new PdfFormImporter().ImportWithQuality(CorpusPath(id), id);
            var theirs = (int)Math.Round(quality.CrypticLabelRatio * quality.FieldCount);
            var ours = document.Sections
                .SelectMany(s => s.Prompts)
                .Count(p => ImportQualityHeuristics.LooksCryptic(p.Label));

            ours.Should().Be(theirs,
                $"{id}: the copied heuristic must agree with the original it was copied from");
        }
    }

    private static string CorpusPath(string id) =>
        Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "corpus", $"{id}.pdf");

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
}
