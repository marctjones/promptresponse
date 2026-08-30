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
            optionCount);
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

/// <summary>One mapped prompt plus PDF characteristics needed by assembly and quality assessment.</summary>
internal readonly record struct PdfImportFieldMapping(
    Prompt Prompt,
    int PageNumber,
    bool HadTooltip,
    bool IsButton,
    int OptionCount);
