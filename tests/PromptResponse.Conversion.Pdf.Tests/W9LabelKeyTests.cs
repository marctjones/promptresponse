using System.Text.Json;
using AwesomeAssertions;
using PromptResponse.Conversion.Pdf;
using PromptResponse.Rendering.Pdf;
using Xunit;

namespace PromptResponse.Conversion.Pdf.Tests;

/// <summary>
/// Grades W-9's label recovery against a hand-read key, because W-9 has no
/// tooltips and therefore had no oracle at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is weaker evidence than <see cref="LabelAgreementTests"/> and must
/// not be read as equivalent.</b> Those grade against <c>/TU</c> text the form's
/// author wrote, which is the right answer by definition. This grades against
/// labels a model transcribed from a render of the page — the same kind of act
/// the converter is performing, judged by the same kind of judge.
/// </para>
/// <para>
/// What keeps it honest rather than circular: the key was read from the
/// rendered image, not from the converter's text extraction or any pipeline
/// output, and it is keyed to widget rectangles the PDF states outright. So it
/// is independent of the thing it grades, without being definitional.
/// </para>
/// <para>
/// It exists because W-9 is the form the whole label gate is built around, and
/// grading it on nothing was worse.
/// </para>
/// </remarks>
public class W9LabelKeyTests
{
    private sealed record Key(string FormId, string Tier, Dictionary<string, string> Labels);

    [Fact]
    public void TheKeyCoversEveryFieldW9Declares()
    {
        // A key that silently omitted fields would flatter recovery by grading
        // only the ones somebody bothered to write down.
        var key = Load();
        var declared = PdfWidgetManifest.Extract(CorpusPath("fed-w9")).Importable
            .Where(e => e.Rect is not null)
            .Select(e => e.FullName)
            .ToList();

        key.Labels.Should().HaveCount(declared.Count);
        key.Labels.Keys.Should().BeEquivalentTo(declared,
            "the key is keyed by field name, so a rename must break it rather than skip fields");
        key.Tier.Should().Be("model-authored",
            "the file must keep saying what kind of evidence it is");
    }

    [Fact]
    public void MostOfW9sFieldsGetTheLabelPrintedOnTheForm()
    {
        var key = Load();
        var recovered = ConversionPipeline.Default().Convert(CorpusPath("fed-w9"), "fed-w9")
            .State.FieldsOrEmpty
            .GroupBy(f => f.Id)
            .ToDictionary(g => g.Key, g => g.First().Label);

        var result = LabelAgreement.Compare(
            "fed-w9",
            [.. key.Labels.Select(kv => (kv.Key, kv.Value))],
            recovered);

        result.Recall.Should().BeGreaterThanOrEqualTo(0.52,
            $"agreed {result.Agreed} of {result.Gradable}; " +
            $"worst: {string.Join(" | ", result.Matches.Where(m => !m.IsAgreement).Take(4).Select(m => $"got '{m.Recovered ?? "(none)"}' want '{m.Expected}'"))}");
        result.Precision.Should().BeGreaterThanOrEqualTo(0.82,
            $"of {result.Attempted} labels recovered, {result.Agreed} match the form");
    }

    [Fact]
    public void TheKeyIsNotJustMatchingTheFormsBoilerplate()
    {
        // The same control the tooltip oracle carries. If scoring each field
        // against the NEXT field's label did as well, this would be measuring
        // W-9's shared vocabulary rather than whether the right question landed
        // on the right field.
        var key = Load();
        var recovered = ConversionPipeline.Default().Convert(CorpusPath("fed-w9"), "fed-w9")
            .State.FieldsOrEmpty.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First().Label);

        var result = LabelAgreement.Compare("fed-w9", [.. key.Labels.Select(kv => (kv.Key, kv.Value))], recovered);

        result.ChanceFloor.Should().BeLessThan(result.Recall,
            $"shifting the key by one field scores {result.ChanceFloor:P0} against the real {result.Recall:P0}");
    }

    private static Key Load() => JsonSerializer.Deserialize<Key>(
        File.ReadAllText(Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "label-keys", "fed-w9.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static string CorpusPath(string id) =>
        Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "corpus", $"{id}.pdf");

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
}
