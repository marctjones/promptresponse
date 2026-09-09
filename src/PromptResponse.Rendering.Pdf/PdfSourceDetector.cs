using Excise.Core.Document;
using PdfeDoc = Excise.Core.Document.PdfDocument;

namespace PromptResponse.Rendering.Pdf;

/// <summary>
/// The places a PDF can offer field information from. These <em>compose</em>: the
/// most useful case is a PDF that carries both, where the AcroForm supplies a
/// complete field list with real round-trippable ids and the text layer supplies
/// the human-readable labels those fields are often missing.
/// </summary>
[Flags]
public enum PdfFieldSources
{
    /// <summary>Neither real form fields nor extractable text — a scan. Needs OCR.</summary>
    None = 0,

    /// <summary>Real AcroForm widgets. Exact field identity and placement.</summary>
    AcroForm = 1,

    /// <summary>An embedded text layer: exact text with exact page coordinates.</summary>
    TextLayer = 2,
}

/// <summary>What a single page offered, and the evidence behind that verdict.</summary>
/// <param name="PageNumber">1-based page number.</param>
/// <param name="ImportableFieldCount">
/// AcroForm fields the importer would actually use — signature fields excluded,
/// since they carry no answer to import.
/// </param>
/// <param name="SignatureFieldCount">Signature fields present but not importable.</param>
/// <param name="CharacterCount">Non-whitespace characters extracted from the page.</param>
/// <param name="Sources">Which sources this page offers.</param>
public sealed record PdfPageEvidence(
    int PageNumber,
    int ImportableFieldCount,
    int SignatureFieldCount,
    int CharacterCount,
    PdfFieldSources Sources);

/// <summary>
/// Which field sources a PDF offers, per page and overall, with the evidence used.
/// </summary>
/// <remarks>
/// Deliberately reports evidence rather than a bare verdict: a caller deciding
/// whether to spend OCR or a model on a document should be able to see <em>why</em>
/// a page was called image-only, and a wrong call is expensive in both directions.
/// </remarks>
/// <param name="Sources">Every source any page offers, unioned.</param>
/// <param name="ImportableFieldCount">
/// AcroForm fields the importer would use, document-wide — signatures excluded.
/// </param>
/// <param name="SignatureFieldCount">Signature fields present but not importable.</param>
/// <param name="CharacterCount">Non-whitespace characters across all pages.</param>
/// <param name="UnplacedImportableFieldCount">
/// Importable fields that belong to no page — non-terminal field-tree nodes
/// (§12.7.3.2), which carry a name and children but no widget. They count toward
/// the document total but cannot be attributed to any page, so
/// <c>ImportableFieldCount</c> is <em>not</em> the sum of the per-page counts
/// whenever this is non-zero.
/// </param>
/// <param name="Pages">Per-page evidence, in page order.</param>
public sealed record PdfSourceReport(
    PdfFieldSources Sources,
    int ImportableFieldCount,
    int SignatureFieldCount,
    int CharacterCount,
    int UnplacedImportableFieldCount,
    IReadOnlyList<PdfPageEvidence> Pages)
{
    /// <summary>The PDF has real AcroForm fields the importer can read directly.</summary>
    public bool HasAcroForm => Sources.HasFlag(PdfFieldSources.AcroForm);

    /// <summary>The PDF carries extractable text with coordinates.</summary>
    public bool HasTextLayer => Sources.HasFlag(PdfFieldSources.TextLayer);

    /// <summary>Neither source: a scan, so pixels are all there is.</summary>
    public bool IsImageOnly => Sources == PdfFieldSources.None;

    /// <summary>
    /// Both sources present. The best case: complete field identity from the
    /// AcroForm, real labels recoverable from the text near each widget.
    /// </summary>
    public bool CanRecoverLabelsFromText => HasAcroForm && HasTextLayer;
}

/// <summary>
/// Decides which field sources a PDF offers, before anything tries to convert it.
/// </summary>
public static class PdfSourceDetector
{
    /// <summary>
    /// Non-whitespace characters a page needs before its text layer counts as usable.
    /// </summary>
    /// <remarks>
    /// Measured across the 11 real federal and Connecticut forms in
    /// <c>scripts/pdf-form-benchmark/corpus/</c>, the split is not close: the one
    /// scanned form (CT DMV A-25) extracts <b>0</b> characters, while every
    /// digitally-authored form yields <b>2,673-31,411</b> for the document. There is
    /// no middle ground to adjudicate, so this sits just clear of zero rather than
    /// at some fraction of the observed range — the point is to ignore a stray mark
    /// or scanner artefact without excluding a genuinely short form, whose handful
    /// of characters is still exact text worth reading.
    /// <para>
    /// Known blind spot: every scanned example available has <em>no</em> OCR layer.
    /// A scan that arrives with one — common, since many scanners add one — would
    /// land above this bar and be reported as having a text layer. That is arguably
    /// right (the text is there to use), but it means "has a text layer" must never
    /// be read as "that text is accurate". Callers wanting that distinction should
    /// look at the reported character counts rather than the flag.
    /// </para>
    /// </remarks>
    public const int MinCharactersForTextLayer = 25;

    /// <summary>Detects the sources offered by a PDF on disk.</summary>
    public static PdfSourceReport Detect(string path)
    {
        using var doc = PdfeDoc.Open(path);
        return Detect(doc);
    }

    /// <summary>Detects the sources offered by PDF bytes.</summary>
    public static PdfSourceReport Detect(byte[] bytes)
    {
        using var doc = PdfeDoc.Open(bytes);
        return Detect(doc);
    }

    private static PdfSourceReport Detect(PdfeDoc doc)
    {
        var importableByPage = new Dictionary<int, int>();
        var signatureByPage = new Dictionary<int, int>();
        var unplacedImportable = 0;
        var totalImportable = 0;
        var totalSignature = 0;

        foreach (var field in doc.GetAcroForm()?.Fields ?? [])
        {
            var isSignature = field.FieldType == PdfFieldType.Signature;
            if (isSignature) totalSignature++; else totalImportable++;

            // A field need not sit on a page: §12.7.3.2 non-terminal nodes in the
            // field tree carry a name and children but no widget (excise's 3.9.4
            // notes count 30 of them in irs-1040.pdf alone). Those are still
            // importable fields, they just cannot be attributed to a page — so
            // count them at the document level rather than silently dropping them
            // or blaming page 0.
            if (field.PageNumber is not { } page)
            {
                if (!isSignature) unplacedImportable++;
                continue;
            }

            var target = isSignature ? signatureByPage : importableByPage;
            target[page] = target.GetValueOrDefault(page) + 1;
        }

        var pages = new List<PdfPageEvidence>();
        for (var pageNumber = 1; pageNumber <= doc.Pages.Count; pageNumber++)
        {
            var importable = importableByPage.GetValueOrDefault(pageNumber);
            var characters = CountVisibleCharacters(doc, pageNumber);

            var sources = PdfFieldSources.None;
            if (importable > 0) sources |= PdfFieldSources.AcroForm;
            if (characters >= MinCharactersForTextLayer) sources |= PdfFieldSources.TextLayer;

            pages.Add(new PdfPageEvidence(
                pageNumber, importable, signatureByPage.GetValueOrDefault(pageNumber), characters, sources));
        }

        // Unplaced fields still mean "this PDF has an AcroForm worth reading",
        // even when no single page can claim them.
        var union = pages.Aggregate(PdfFieldSources.None, (acc, p) => acc | p.Sources);
        if (unplacedImportable > 0) union |= PdfFieldSources.AcroForm;

        return new PdfSourceReport(
            union,
            totalImportable,
            totalSignature,
            pages.Sum(p => p.CharacterCount),
            unplacedImportable,
            pages);
    }

    /// <summary>
    /// Non-whitespace characters on a page. Whitespace is excluded because a page
    /// of positioned-but-empty text runs would otherwise read as having content.
    /// A page that cannot be read at all counts as zero rather than failing the
    /// whole detection — one unreadable page should not decide the document.
    /// </summary>
    private static int CountVisibleCharacters(PdfeDoc doc, int pageNumber)
    {
        try
        {
            return doc.GetPage(pageNumber).Text.Count(c => !char.IsWhiteSpace(c));
        }
        catch (Exception)
        {
            return 0;
        }
    }
}
