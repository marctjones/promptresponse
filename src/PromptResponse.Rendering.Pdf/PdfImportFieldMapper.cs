using Excise.Core.Document;
using PromptResponse.Core.Models;

namespace PromptResponse.Rendering.Pdf;

/// <summary>Maps one AcroForm field to an APR prompt and captures its review signals.</summary>
internal static class PdfImportFieldMapper
{
    public static PdfImportFieldMapping Map(PdfField field, int ordinal, HashSet<string> seenIds)
    {
        var tooltip = TrimToNull(field.RawDictionary.GetStringOrNull("TU"));
        var fieldName = TrimToNull(field.PartialName) ?? TrimToNull(field.FullName);
        // Prefer the accessible-name tooltip, then the PDF field name, then a stable fallback.
        var label = tooltip ?? fieldName ?? $"Field {ordinal}";
        var hints = CreateHints(field, out var optionCount);

        if (tooltip != null && fieldName != null && !string.Equals(tooltip, fieldName, StringComparison.Ordinal))
        {
            hints.HelpText = $"PDF field: {fieldName}";
        }

        var prompt = new Prompt
        {
            Id = UniqueId(TrimToNull(field.FullName) ?? $"field-{ordinal}", seenIds),
            Label = label,
            Response = string.Empty,
            Hints = hints,
        };

        return new PdfImportFieldMapping(
            prompt,
            field.PageNumber ?? 0,
            HadTooltip: tooltip != null,
            IsButton: field.FieldType == PdfFieldType.Button,
            optionCount,
            Origin: PdfFieldOrigin.AcroFormWidget,
            TargetRect: field.Rect);
    }

    private static PromptHints CreateHints(PdfField field, out int optionCount)
    {
        optionCount = 0;
        var hints = new PromptHints();
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
                    hints.SuggestedValues = options.Where(option => !string.IsNullOrWhiteSpace(option)).ToList();
                }
                break;
            case PdfFieldType.Text:
            default:
                hints.ExpectedDataType = field.IsMultiline ? "multiline" : "text";
                break;
        }

        return hints;
    }

    private static string UniqueId(string candidate, HashSet<string> seen)
    {
        var id = candidate;
        for (var suffix = 2; !seen.Add(id); suffix++)
        {
            id = $"{candidate}#{suffix}";
        }

        return id;
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Where a discovered field came from.</summary>
/// <remarks>
/// Milestone #49 feeds one assembly path from several discovery sources. Without
/// recording which one produced a field, every metric downstream is an
/// undifferentiated total: #427 exists because an end-to-end score says "bad"
/// without saying which phase was bad, and provenance is the minimum needed to
/// attribute a result to a phase.
/// </remarks>
internal enum PdfFieldOrigin
{
    /// <summary>A real AcroForm widget — exact identity, exact placement.</summary>
    AcroFormWidget,

    /// <summary>Inferred from the embedded text layer and its coordinates (#422).</summary>
    TextLayer,

    /// <summary>Recovered by OCR over a rendered page — the image-only fallback.</summary>
    Ocr,
}

/// <summary>One mapped prompt plus PDF characteristics needed by assembly and quality assessment.</summary>
/// <param name="Prompt">The APR prompt this field becomes.</param>
/// <param name="PageNumber">1-based page, or 0 when the field belongs to no page.</param>
/// <param name="HadTooltip">Whether the source supplied a human-readable name (<c>/TU</c>).</param>
/// <param name="IsButton">Whether this is a checkbox or radio button rather than a value field.</param>
/// <param name="OptionCount">Choice options offered, if any.</param>
/// <param name="Origin">Which discovery source produced this field.</param>
/// <param name="TargetRect">
/// Where the answer goes on the page, in PDF user space — the widget
/// <c>/Rect</c> for an AcroForm field, a drawn blank or checkbox for a
/// text-layer field (#424). Null when the source could not place it.
/// <para>
/// Carried even though APR is layout-free and never emits it, because two
/// things need it and neither can recover it later: the label↔input pairing
/// shared by #426 and #424, and the geometry-matched oracle that grades a
/// converted flat form against its source PDF's widget rects.
/// </para>
/// </param>
internal readonly record struct PdfImportFieldMapping(
    Prompt Prompt,
    int PageNumber,
    bool HadTooltip,
    bool IsButton,
    int OptionCount,
    PdfFieldOrigin Origin = PdfFieldOrigin.AcroFormWidget,
    PdfRectangle? TargetRect = null);
