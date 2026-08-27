using System.Text;
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

    /// <summary>Per-field signals collected during mapping, used to score the import.</summary>
    private readonly record struct Mapped(Prompt Prompt, bool HadTooltip, bool IsButton, int OptionCount);

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
        var sectionsByPage = new SortedDictionary<int, Section>();
        var signals = new List<Mapped>();
        var fieldIndex = 0;

        foreach (var field in form.Fields)
        {
            if (field.FieldType == PdfFieldType.Signature)
            {
                continue; // not a fillable data field
            }

            var mapped = MapField(field, ++fieldIndex, seenPromptIds);
            signals.Add(mapped);

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
            section.Prompts.Add(mapped.Prompt);
        }

        var sections = sectionsByPage.Values.Where(s => s.Prompts.Count > 0).ToList();
        if (sections.Count == 0)
        {
            throw new NoFormFieldsException("The PDF's only fields were signatures, which are not imported.");
        }

        var document = new AprDocument
        {
            Version = AprFormat.CurrentVersion,
            DocumentType = DocumentType.Template,
            Metadata = new Metadata
            {
                Title = string.IsNullOrWhiteSpace(title) ? "Imported form" : title,
                Description = "Imported from a fillable PDF (AcroForm). Review labels and field types.",
                TemplateId = Slug(title),
                TemplateVersion = AprFormat.CurrentVersion,
            },
            Sections = sections,
        };

        return (document, AssessQuality(signals));
    }

    private static Mapped MapField(PdfField field, int ordinal, HashSet<string> seenIds)
    {
        var tooltip = TrimToNull(field.RawDictionary.GetStringOrNull("TU"));
        var fieldName = TrimToNull(field.PartialName) ?? TrimToNull(field.FullName);

        // A human label is the most valuable thing to preserve: prefer the
        // accessible-name tooltip, then the field's name, then a stable fallback.
        var label = tooltip ?? fieldName ?? $"Field {ordinal}";

        var id = UniqueId(TrimToNull(field.FullName) ?? $"field-{ordinal}", seenIds);

        var hints = new PromptHints();
        var optionCount = 0;
        switch (field.FieldType)
        {
            case PdfFieldType.Button:
                hints.ExpectedDataType = "boolean";
                optionCount = field.Options?.Count ?? 0;
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

        var prompt = new Prompt { Id = id, Label = label, Response = string.Empty, Hints = hints };
        return new Mapped(prompt, HadTooltip: tooltip != null, IsButton: field.FieldType == PdfFieldType.Button, optionCount);
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

    /// <summary>
    /// Scores the import from cheap, no-AI signals: how many labels came from PDF
    /// tooltips vs degraded to raw field names, plus duplicate labels and
    /// likely-radio-group checkboxes. Drives a use-directly / review / use-skill
    /// recommendation.
    /// </summary>
    private static ImportQuality AssessQuality(IReadOnlyList<Mapped> signals)
    {
        var total = signals.Count;
        var flags = new List<FieldFlag>();

        var labelCounts = signals
            .GroupBy(s => s.Prompt.Label, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var withTooltip = 0;
        var cryptic = 0;

        foreach (var s in signals)
        {
            if (s.HadTooltip) withTooltip++;

            var label = s.Prompt.Label;
            if (IsCrypticLabel(label))
            {
                cryptic++;
                flags.Add(new FieldFlag(s.Prompt.Id, label, FieldFlagKind.CrypticLabel,
                    "Label looks like a raw PDF field name, not a question."));
            }
            else if (labelCounts[label] > 1)
            {
                flags.Add(new FieldFlag(s.Prompt.Id, label, FieldFlagKind.DuplicateLabel,
                    $"Label is shared by {labelCounts[label]} fields."));
            }

            if (s.IsButton && s.OptionCount > 0)
            {
                flags.Add(new FieldFlag(s.Prompt.Id, label, FieldFlagKind.AmbiguousChoice,
                    "Checkbox/button carries options — likely a radio group that should be a dropdown."));
            }
        }

        var tooltipCoverage = total == 0 ? 0 : (double)withTooltip / total;
        var crypticRatio = total == 0 ? 0 : (double)cryptic / total;
        var dupCount = signals.Count(s => labelCounts[s.Prompt.Label] > 1);
        var dupRatio = total == 0 ? 0 : (double)dupCount / total;

        // Non-cryptic labels are the headline of quality; duplicates apply a smaller
        // penalty; ambiguous radio groups are flagged but don't move the score.
        var dupPenalty = (int)Math.Round(Math.Min(15, dupRatio * 30));
        var score = (int)Math.Clamp(Math.Round((1 - crypticRatio) * 100) - dupPenalty, 0, 100);

        var grade = score >= 90 ? "A" : score >= 75 ? "B" : score >= 60 ? "C" : score >= 40 ? "D" : "F";
        var recommendation =
            score >= 70 ? ImportRecommendation.UseDirectly :
            score >= 40 ? ImportRecommendation.ReviewRecommended :
                          ImportRecommendation.UseSkillInstead;

        var readablePct = (int)Math.Round((1 - crypticRatio) * 100);
        var summary = recommendation switch
        {
            ImportRecommendation.UseDirectly =>
                $"Good ({score}/100, {grade}). {readablePct}% of {total} fields have human-readable labels — use directly.",
            ImportRecommendation.ReviewRecommended =>
                $"Fair ({score}/100, {grade}). {readablePct}% of {total} fields have readable labels — review before sharing.",
            _ =>
                $"Poor ({score}/100, {grade}). Only {readablePct}% of {total} fields have readable labels " +
                "(the PDF lacks field tooltips) — use the document-to-apr skill, or run it to enrich this import.",
        };

        return new ImportQuality(score, grade, recommendation, summary, total,
            tooltipCoverage, crypticRatio, dupRatio, flags);
    }

    /// <summary>True when a label looks like a raw AcroForm field name (e.g. <c>f1_1[0]</c>).</summary>
    private static bool IsCrypticLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return true;
        var t = label.Trim();
        if (t.Contains('[')) return true;     // array index: f1_1[0], TextField11[1], #field[37]
        if (t.StartsWith('#')) return true;   // #field…
        // a short token with letters+digits and no spaces: f1_1, c1_2, p3
        if (!t.Contains(' ') && t.Length <= 12 && t.Any(char.IsDigit) && t.Any(char.IsLetter)) return true;
        return false;
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
