using System.Text.Json;
using AwesomeAssertions;
using PromptResponse.Conversion.Pdf;
using PromptResponse.Rendering.Pdf;
using Xunit;

namespace PromptResponse.Conversion.Pdf.Tests;

/// <summary>
/// Grades label recovery against hand-read keys, for the forms whose authors
/// wrote no tooltips and which therefore had no oracle at all.
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
/// <para>
/// <b>fed-8822 and fed-ss4 are held out.</b> Every pairing rule in the pipeline
/// was derived from W-9 and I-9, so those two say only that the rules fit the
/// forms they were fitted to. These two were keyed afterwards and tuned against
/// never, which is the only evidence available that the rules describe forms
/// rather than describing two documents.
/// </para>
/// </remarks>
public class HandReadKeyTests
{
    private sealed record Key(string FormId, string Tier, Dictionary<string, string> Labels);

    /// <summary>Form, minimum label recall, minimum label precision.</summary>
    /// <remarks>
    /// Floors sit just under measured. fed-ss4 is lower because it is the
    /// densest form in the corpus -- 89 fields, most of them tick boxes in tight
    /// grids -- and that gap is the honest cost of rules fitted on two forms.
    /// </remarks>
    public static TheoryData<string, double, double> Keys() => new()
    {
        { "fed-w9", 0.78, 0.85 },
        { "fed-8822", 0.75, 0.90 },
        { "fed-ss4", 0.58, 0.80 },
    };

    [Theory]
    [MemberData(nameof(Keys))]
    public void TheKeyCoversEveryFieldTheFormDeclares(string id, double minRecall, double minPrecision)
    {
        _ = minRecall;
        _ = minPrecision;
        // A key that silently omitted fields would flatter recovery by grading
        // only the ones somebody bothered to write down.
        var key = Load(id);
        var declared = PdfWidgetManifest.Extract(CorpusPath(id)).Importable
            .Where(e => e.Rect is not null)
            .Select(e => e.FullName)
            .ToList();

        key.Labels.Should().HaveCount(declared.Count);
        key.Labels.Keys.Should().BeEquivalentTo(declared,
            "the key is keyed by field name, so a rename must break it rather than skip fields");
        key.Tier.Should().Be("model-authored",
            "the file must keep saying what kind of evidence it is");
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public void MostFieldsGetTheLabelPrintedOnTheForm(string id, double minRecall, double minPrecision)
    {
        var key = Load(id);
        var recovered = ConversionPipeline.Default().Convert(CorpusPath(id), id)
            .State.FieldsOrEmpty
            .GroupBy(f => f.Id)
            .ToDictionary(g => g.Key, g => g.First().Label);

        var result = LabelAgreement.Compare(
            id,
            [.. key.Labels.Select(kv => (kv.Key, kv.Value))],
            recovered);

        result.Recall.Should().BeGreaterThanOrEqualTo(minRecall,
            $"agreed {result.Agreed} of {result.Gradable}; " +
            $"worst: {string.Join(" | ", result.Matches.Where(m => !m.IsAgreement).Take(4).Select(m => $"got '{m.Recovered ?? "(none)"}' want '{m.Expected}'"))}");
        result.Precision.Should().BeGreaterThanOrEqualTo(minPrecision,
            $"of {result.Attempted} labels recovered, {result.Agreed} match the form");
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public void TheKeyIsNotJustMatchingTheFormsBoilerplate(string id, double minRecall, double minPrecision)
    {
        _ = minRecall;
        _ = minPrecision;
        // The same control the tooltip oracle carries. If scoring each field
        // against the NEXT field's label did as well, this would be measuring
        // W-9's shared vocabulary rather than whether the right question landed
        // on the right field.
        var key = Load(id);
        var recovered = ConversionPipeline.Default().Convert(CorpusPath(id), id)
            .State.FieldsOrEmpty.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First().Label);

        var result = LabelAgreement.Compare(id, [.. key.Labels.Select(kv => (kv.Key, kv.Value))], recovered);

        result.ChanceFloor.Should().BeLessThan(result.Recall,
            $"shifting the key by one field scores {result.ChanceFloor:P0} against the real {result.Recall:P0}");
    }

    private static Key Load(string id) => JsonSerializer.Deserialize<Key>(
        File.ReadAllText(Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "label-keys", $"{id}.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static string CorpusPath(string id) =>
        Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "corpus", $"{id}.pdf");

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
}
