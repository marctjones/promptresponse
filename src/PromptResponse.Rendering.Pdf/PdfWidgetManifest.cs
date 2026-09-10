using Excise.Core.Document;
using PromptResponse.Core.Models;
using PdfeDoc = Excise.Core.Document.PdfDocument;

namespace PromptResponse.Rendering.Pdf;

/// <summary>One AcroForm field, as the PDF itself describes it.</summary>
/// <param name="FullName">
/// The fully qualified field name (<c>/T</c> joined up the field tree). This is the
/// field's machine identity: <see cref="PdfImportFieldMapper"/> uses it verbatim as
/// the APR prompt id, which is what lets a filled value round-trip back into the
/// source PDF.
/// </param>
/// <param name="FieldType">Text, Button, Choice, Signature.</param>
/// <param name="PageNumber">1-based page, or null for a non-terminal field-tree node.</param>
/// <param name="Tooltip">
/// The field's <c>/TU</c> alternate name, or null when it has none. This is the
/// form author's own human-readable label, and where it exists it is the one
/// Tier 1 answer available for "what should this prompt say" — nobody inferred
/// it, the form states it. Only 34% of the corpus's 446 importable fields carry
/// one, which is what makes label recovery (#426) necessary for the rest.
/// </param>
/// <param name="OptionCount">Choice options offered, if any.</param>
/// <param name="IsPushButton">
/// Whether this is a JavaScript action button rather than a field. Recorded
/// rather than dropped so the exclusion stays visible in the manifest.
/// </param>
/// <param name="Rect">
/// Where the widget sits on its page, in PDF user space. This is the half of
/// the answer key that survives flattening: a converter reading a flattened
/// form cannot know the field's name, but it can find the blank, so geometry
/// is the only key that turns for the derived fixtures.
/// </param>
public sealed record WidgetManifestEntry(
    string FullName,
    PdfFieldType FieldType,
    int? PageNumber,
    int OptionCount,
    bool IsPushButton = false,
    PdfRectangle? Rect = null,
    string? Tooltip = null)
{
    /// <summary>Whether the field arrived with a human-readable name.</summary>
    public bool HasTooltip => !string.IsNullOrWhiteSpace(Tooltip);

    /// <summary>
    /// Whether the importer should produce a prompt for this field — signature
    /// fields and push buttons carry no answer. Mirrors
    /// <see cref="PdfImportableField.CarriesAnAnswer"/>, which is the single
    /// definition both sides read.
    /// </summary>
    public bool IsImportable => FieldType != PdfFieldType.Signature && !IsPushButton;
}

/// <summary>
/// The complete list of AcroForm fields a PDF declares — a <em>mechanical</em> oracle
/// for "did the conversion lose a field?".
/// </summary>
/// <remarks>
/// This is the strongest kind of reference data available to this project, and it is
/// free: it involves no human judgement and no model, because the PDF's own field
/// list is definitionally the right answer to "what fields does this PDF have". Every
/// form in the benchmark corpus that carries an AcroForm — which, as it turns out, is
/// all 11 of the scored ones — can therefore be checked for field completeness at
/// zero cost and with no answer key to maintain.
/// <para>
/// Two things it deliberately is not. It says nothing about whether a field's
/// <em>label</em> is any good — that is exactly what <see cref="ImportQuality"/>
/// measures, and what the cryptic-name case needs recovering. And it cannot judge a
/// PDF with no AcroForm at all, which is the converter's actual target; for those,
/// completeness needs a different reference.
/// </para>
/// </remarks>
public sealed record WidgetManifest(IReadOnlyList<WidgetManifestEntry> Entries)
{
    /// <summary>Fields the importer would actually produce a prompt for.</summary>
    public IReadOnlyList<WidgetManifestEntry> Importable { get; } =
        [.. Entries.Where(e => e.IsImportable)];

    /// <summary>Signature fields, excluded from importable — reported so the exclusion is visible.</summary>
    /// <remarks>
    /// Counted directly rather than as <c>Entries.Count - Importable.Count</c>:
    /// that difference is every excluded field, and push buttons are excluded
    /// too, so the subtraction would silently report them as signatures.
    /// </remarks>
    public int SignatureCount => Entries.Count(e => e.FieldType == PdfFieldType.Signature);

    /// <summary>Push-button actions, excluded from importable — reported so the exclusion is visible.</summary>
    public int PushButtonCount => Entries.Count(e => e.IsPushButton);

    /// <summary>How many importable fields arrived with a <c>/TU</c> human-readable name.</summary>
    public int TooltipCount => Importable.Count(e => e.HasTooltip);
}

/// <summary>
/// How completely a converted document accounts for the fields its source PDF declared.
/// </summary>
/// <param name="Expected">Importable fields the PDF declared.</param>
/// <param name="Covered">Those for which the document has a prompt.</param>
/// <param name="MissingFieldNames">
/// Declared fields with no corresponding prompt — a conversion that silently dropped
/// something a person is expected to fill in.
/// </param>
/// <param name="UnaccountedPromptIds">
/// Prompts that match no declared field. Not necessarily wrong — a converter reading
/// the printed page may legitimately find something the AcroForm omits — but on a
/// pure AcroForm import it means an invented field.
/// </param>
/// <param name="Matches">
/// Which prompt was placed on which declared field. Empty for name-based
/// comparison, populated by <see cref="PdfGeometryCoverage.Compare"/>.
/// </param>
public sealed record WidgetCoverage(
    int Expected,
    int Covered,
    IReadOnlyList<string> MissingFieldNames,
    IReadOnlyList<string> UnaccountedPromptIds,
    IReadOnlyList<GeometryMatch>? Matches = null)
{
    /// <summary>Field-to-prompt pairings established by position, never null.</summary>
    public IReadOnlyList<GeometryMatch> MatchesOrEmpty => Matches ?? [];

    /// <summary>Of the prompts placed, the fraction that landed on a declared field.</summary>
    /// <remarks>
    /// The precision half. <see cref="Fraction"/> is recall and rises when a
    /// converter simply guesses more, so the two must always be read together.
    /// </remarks>
    public double Precision
    {
        get
        {
            var placed = Covered + UnaccountedPromptIds.Count;
            return placed == 0 ? 0 : (double)Covered / placed;
        }
    }

    /// <summary>Recall and precision combined, weighting recall <paramref name="beta"/> times as heavily.</summary>
    /// <remarks>
    /// Beta above 1 favours finding everything, which is the right bias for
    /// field <em>discovery</em>: a missed field cannot be recovered by any later
    /// phase, because nothing downstream re-reads the page, while a spurious one
    /// can still be pruned. Beta of 1 is the right bias for the finished
    /// document, where both errors are equally visible to the person filling it.
    /// </remarks>
    public double FScore(double beta = 1.0)
    {
        var (p, r) = (Precision, Fraction);
        var b2 = beta * beta;
        return p + r == 0 ? 0 : (1 + b2) * p * r / ((b2 * p) + r);
    }

    /// <summary>Fraction of declared fields accounted for, 0-1. Vacuously 1 when the PDF declared none.</summary>
    public double Fraction => Expected == 0 ? 1.0 : (double)Covered / Expected;

    /// <summary>Nothing the PDF declared went missing.</summary>
    public bool IsComplete => MissingFieldNames.Count == 0;
}

/// <summary>Reads a PDF's own field list, and checks a conversion against it.</summary>
public static class PdfWidgetManifest
{
    /// <summary>Reads the field manifest from a PDF on disk.</summary>
    public static WidgetManifest Extract(string path)
    {
        using var doc = PdfeDoc.Open(path);
        return Extract(doc);
    }

    /// <summary>Reads the field manifest from PDF bytes.</summary>
    public static WidgetManifest Extract(byte[] bytes)
    {
        using var doc = PdfeDoc.Open(bytes);
        return Extract(doc);
    }

    private static WidgetManifest Extract(PdfeDoc doc) => new(
    [
        .. (doc.GetAcroForm()?.Fields ?? []).Select(f => new WidgetManifestEntry(
            f.FullName ?? string.Empty,
            f.FieldType,
            f.PageNumber,
            OptionCount: f.Options?.Count ?? 0,
            IsPushButton: f.IsPushButton,
            Rect: f.Rect,
            Tooltip: Trimmed(f.RawDictionary.GetStringOrNull("TU")))),
    ]);

    /// <summary>
    /// Checks a converted document against its source PDF's field list.
    /// </summary>
    /// <remarks>
    /// Matching is by prompt id against field name, because
    /// <see cref="PdfImportFieldMapper"/> uses the fully qualified field name verbatim
    /// as the id — appending only a <c>#2</c>-style suffix when two fields share a
    /// name, which this strips before comparing. Counts are compared as multisets
    /// rather than sets, since a PDF may legitimately declare the same fully qualified
    /// name more than once and dropping duplicates would hide a real loss.
    /// </remarks>
    public static WidgetCoverage Compare(WidgetManifest manifest, AprDocument document)
    {
        var expected = manifest.Importable.Select(e => e.FullName).ToList();
        var promptIds = document.Sections
            .SelectMany(s => s.Prompts)
            .Select(p => StripDisambiguator(p.Id))
            .ToList();

        var remaining = new List<string>(promptIds);
        var missing = new List<string>();
        foreach (var name in expected)
        {
            var at = remaining.IndexOf(name);
            if (at >= 0) remaining.RemoveAt(at); else missing.Add(name);
        }

        return new WidgetCoverage(expected.Count, expected.Count - missing.Count, missing, remaining);
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Removes the <c>#2</c>/<c>#3</c> suffix the importer appends to make a repeated
    /// field name unique as an APR id.
    /// </summary>
    private static string StripDisambiguator(string id)
    {
        var hash = id.LastIndexOf('#');
        return hash > 0 && id.AsSpan(hash + 1).ToString() is { Length: > 0 } tail
                        && tail.All(char.IsDigit)
            ? id[..hash]
            : id;
    }
}
