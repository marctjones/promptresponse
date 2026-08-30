using Excise.Core.Document;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using PdfeDoc = Excise.Core.Document.PdfDocument;

namespace PromptResponse.Rendering.Pdf;

/// <summary>
/// Imports an existing <em>fillable</em> PDF (one with AcroForm fields) into an
/// APR template by reading its form fields via pdfe. This is the deterministic,
/// machine-readable path — it only sees real AcroForm widgets, so it does nothing
/// for a flat/printed/scanned PDF (use the <c>document-to-apr</c> skill for those).
/// </summary>
/// <remarks>
/// Each AcroForm field becomes a prompt; fields are grouped into one section per
/// page. Field kinds map to advisory data-type hints (text/checkbox→boolean/
/// choice→suggestedValues); the field's tooltip (<c>/TU</c>) becomes the label
/// (and a person-readable label is the most valuable thing a real form carries),
/// falling back to the field's own name. Signature fields are skipped. The result
/// is a valid template (unique ids, non-empty labels/section-titles).
/// </remarks>
public sealed class PdfFormImporter
{
    /// <summary>Raised when the PDF has no AcroForm fields to import.</summary>
    public sealed class NoFormFieldsException(string message) : Exception(message);

    /// <summary>Imports from a file path.</summary>
    public AprDocument Import(string path, string? title = null) => ImportWithQuality(path, title).Document;

    /// <summary>Imports from PDF bytes.</summary>
    public AprDocument Import(byte[] bytes, string? title = null) => ImportWithQuality(bytes, title).Document;

    /// <summary>Imports and also returns a heuristic <see cref="ImportQuality"/> assessment.</summary>
    public (AprDocument Document, ImportQuality Quality) ImportWithQuality(string path, string? title = null)
    {
        using var doc = PdfeDoc.Open(path);
        return Import(doc, title ?? TitleFromFileName(path));
    }

    /// <summary>Imports from PDF bytes and also returns a quality assessment.</summary>
    public (AprDocument Document, ImportQuality Quality) ImportWithQuality(byte[] bytes, string? title = null)
    {
        using var doc = PdfeDoc.Open(bytes);
        return Import(doc, title ?? "Imported form");
    }

    private static (AprDocument Document, ImportQuality Quality) Import(PdfeDoc doc, string title)
    {
        var form = doc.GetAcroForm();
        if (form is null || form.Fields.Count == 0)
        {
            throw new NoFormFieldsException(
                "This PDF has no fillable AcroForm fields, so there is nothing to import. " +
                "It is likely a flat/printed/scanned form — use the 'document-to-apr' skill instead.");
        }

        var seenPromptIds = new HashSet<string>(StringComparer.Ordinal);
        var mappings = new List<PdfImportFieldMapping>();
        var fieldIndex = 0;

        foreach (var field in form.Fields)
        {
            if (field.FieldType == PdfFieldType.Signature)
            {
                continue; // not a fillable data field
            }

            mappings.Add(PdfImportFieldMapper.Map(field, ++fieldIndex, seenPromptIds));
        }

        if (mappings.Count == 0)
        {
            throw new NoFormFieldsException("The PDF's only fields were signatures, which are not imported.");
        }

        return (PdfImportDocumentBuilder.Build(title, mappings), PdfImportQualityAssessor.Assess(mappings));
    }

    private static string TitleFromFileName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return string.IsNullOrWhiteSpace(name) ? "Imported form" : name;
    }

}
