using AwesomeAssertions;
using PromptResponse.Conversion.Pdf;
using Xunit;

namespace PromptResponse.Conversion.Pdf.Tests;

/// <summary>
/// The last phase before a model would see anything. Everything the
/// deterministic phases recovered has to survive into the document, or the work
/// was done and thrown away.
/// </summary>
public class AssembleFidelityTests
{
    public static TheoryData<string> Forms() => ["fed-w9", "fed-i9", "fed-w9-flat", "fed-i9-flat", "ct-w4"];

    [Theory]
    [MemberData(nameof(Forms))]
    public void EveryFieldAndLabelSurvivesIntoTheDocument(string id)
    {
        var state = ConversionPipeline.Default().Convert(CorpusPath(id), id).State;
        var fields = state.FieldsOrEmpty;
        state.Document.Should().NotBeNull();

        var prompts = state.Document!.Sections
            .SelectMany(s => s.Prompts)
            .ToDictionary(p => p.Id, p => p, StringComparer.Ordinal);

        prompts.Should().HaveCount(fields.Count, "assembly must not drop or invent a field");
        foreach (var field in fields.Where(f => f.HasLabel))
        {
            prompts.Should().ContainKey(field.Id);
            prompts[field.Id].Label.Should().Be(field.Label,
                $"{id}: a label recovered from the page must reach the document unchanged");
        }
    }

    [Theory]
    [MemberData(nameof(Forms))]
    public void TheFormsOwnGuidanceReachesThePromptItExplains(string id)
    {
        // The head/tail split exists so instructions are preserved rather than
        // discarded as prose. It was computing HelpText and dropping it on the
        // floor here, which made the whole split pointless.
        var state = ConversionPipeline.Default().Convert(CorpusPath(id), id).State;
        var withGuidance = state.FieldsOrEmpty.Where(f => !string.IsNullOrWhiteSpace(f.HelpText)).ToList();

        var prompts = state.Document!.Sections
            .SelectMany(s => s.Prompts)
            .ToDictionary(p => p.Id, p => p, StringComparer.Ordinal);

        foreach (var field in withGuidance)
        {
            prompts[field.Id].Hints?.HelpText.Should().Be(field.HelpText,
                $"{id}: {field.Id} carries the form's own guidance and it must not be lost");
        }
    }

    [Fact]
    public void APromptWithNoRecoveredLabelIsReportedRatherThanCountedAsFound()
    {
        // APR requires a non-empty label, so an unlabelled field ships with its
        // id in the label slot -- "p1-389-604-8" as a question. That is
        // defensible only if the phase says how often it happened; a bare
        // "43 prompt(s)" reads as 43 usable questions and is the exact shape of
        // a green check that measures nothing.
        var result = ConversionPipeline.Default().Convert(CorpusPath("fed-w9-flat"), "flat");
        var assemble = result.Phases.Single(p => p.Name == "assemble");
        var unlabelled = result.State.FieldsOrEmpty.Count(f => !f.HasLabel);

        unlabelled.Should().BeGreaterThan(0, "this fixture has fields whose label was not recovered");
        assemble.Detail.Should().Contain(unlabelled.ToString());
        assemble.Detail.Should().Contain("placeholder");
    }

    private static string CorpusPath(string id) =>
        Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "corpus", $"{id}.pdf");

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
}
