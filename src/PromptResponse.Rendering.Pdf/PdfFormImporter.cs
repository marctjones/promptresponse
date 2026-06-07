using System.Text;
using Pdfe.Core.Document;
using PromptResponse.Core.Models;
using PdfeDoc = Pdfe.Core.Document.PdfDocument;

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
    public AprDocument Import(string path, string? title = null)
    {
        using var doc = PdfeDoc.Open(path);
        return Import(doc, title ?? TitleFromFileName(path));
    }

    /// <summary>Imports from PDF bytes.</summary>
    public AprDocument Import(byte[] bytes, string? title = null)
    {
        using var doc = PdfeDoc.Open(bytes);
        return Import(doc, title ?? "Imported form");
    }

    private static AprDocument Import(PdfeDoc doc, string title)
    {
        var form = doc.GetAcroForm();
        if (form is null || form.Fields.Count == 0)
        {
            throw new NoFormFieldsException(
                "This PDF has no fillable AcroForm fields, so there is nothing to import. " +
                "It is likely a flat/printed/scanned form — use the 'document-to-apr' skill instead.");
        }

        var seenPromptIds = new HashSet<string>(StringComparer.Ordinal);
        var sectionsByPage = new SortedDictionary<int, Section>();
        var fieldIndex = 0;

        foreach (var field in form.Fields)
        {
            if (field.FieldType == PdfFieldType.Signature)
            {
                continue; // not a fillable data field
            }

            var prompt = MapField(field, ++fieldIndex, seenPromptIds);
            var page = field.PageNumber ?? 0;
            if (!sectionsByPage.TryGetValue(page, out var section))
            {
                section = new Section
                {
                    Id = page > 0 ? $"page-{page}" : "fields",
                    Title = page > 0 ? $"Page {page}" : "Fields",
                    Description = page > 0 ? $"Fields from page {page} of the source PDF." : null,
                };
                sectionsByPage[page] = section;
            }
            section.Prompts.Add(prompt);
        }

        var sections = sectionsByPage.Values.Where(s => s.Prompts.Count > 0).ToList();
        if (sections.Count == 0)
        {
            throw new NoFormFieldsException("The PDF's only fields were signatures, which are not imported.");
        }

        return new AprDocument
        {
            Version = "1.0",
            DocumentType = DocumentType.Template,
            Metadata = new Metadata
            {
                Title = string.IsNullOrWhiteSpace(title) ? "Imported form" : title,
                Description = "Imported from a fillable PDF (AcroForm). Review labels and field types.",
                TemplateId = Slug(title),
                TemplateVersion = "1.0",
            },
            Sections = sections,
        };
    }

    private static Prompt MapField(PdfField field, int ordinal, HashSet<string> seenIds)
    {
        var tooltip = TrimToNull(field.RawDictionary.GetStringOrNull("TU"));
        var fieldName = TrimToNull(field.PartialName) ?? TrimToNull(field.FullName);

        // A human label is the most valuable thing to preserve: prefer the
        // accessible-name tooltip, then the field's name, then a stable fallback.
        var label = tooltip ?? fieldName ?? $"Field {ordinal}";

        var id = UniqueId(TrimToNull(field.FullName) ?? $"field-{ordinal}", seenIds);

        var hints = new PromptHints();
        switch (field.FieldType)
        {
            case PdfFieldType.Button:
                hints.ExpectedDataType = "boolean";
                break;
            case PdfFieldType.Choice:
                hints.ExpectedDataType = "text";
                if (field.Options is { Count: > 0 } options)
                {
                    hints.SuggestedValues = options.Where(o => !string.IsNullOrWhiteSpace(o)).ToList();
                }
                break;
            case PdfFieldType.Text:
            default:
                hints.ExpectedDataType = field.IsMultiline ? "multiline" : "text";
                break;
        }

        // If the tooltip became the label, the raw field name can still help a
        // reviewer correlate back to the PDF.
        if (tooltip != null && fieldName != null && !string.Equals(tooltip, fieldName, StringComparison.Ordinal))
        {
            hints.HelpText = $"PDF field: {fieldName}";
        }

        return new Prompt { Id = id, Label = label, Response = string.Empty, Hints = hints };
    }

    private static string UniqueId(string candidate, HashSet<string> seen)
    {
        var baseId = candidate;
        var id = baseId;
        var n = 2;
        while (!seen.Add(id))
        {
            id = $"{baseId}#{n++}";
        }
        return id;
    }

    private static string? TrimToNull(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string TitleFromFileName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return string.IsNullOrWhiteSpace(name) ? "Imported form" : name;
    }

    private static string Slug(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        return sb.ToString().Trim('-') is { Length: > 0 } slug ? slug : "imported-form";
    }
}
