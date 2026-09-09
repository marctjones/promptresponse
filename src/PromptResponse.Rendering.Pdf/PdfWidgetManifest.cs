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
/// <param name="HasTooltip">Whether <c>/TU</c> is present — i.e. whether the field arrives with a human-readable name.</param>
/// <param name="OptionCount">Choice options offered, if any.</param>
public sealed record WidgetManifestEntry(
    string FullName,
    PdfFieldType FieldType,
    int? PageNumber,
    bool HasTooltip,
    int OptionCount)
{
    /// <summary>Signature fields carry no answer, so the importer skips them.</summary>
    public bool IsImportable => FieldType != PdfFieldType.Signature;
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
    public int SignatureCount => Entries.Count - Importable.Count;

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
public sealed record WidgetCoverage(
    int Expected,
    int Covered,
    IReadOnlyList<string> MissingFieldNames,
    IReadOnlyList<string> UnaccountedPromptIds)
{
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
            HasTooltip: !string.IsNullOrWhiteSpace(f.RawDictionary.GetStringOrNull("TU")),
            OptionCount: f.Options?.Count ?? 0)),
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
