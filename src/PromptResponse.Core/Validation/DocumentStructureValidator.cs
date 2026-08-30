using PromptResponse.Core.Models;

namespace PromptResponse.Core.Validation;

/// <summary>Validates recursive section, prompt, and advisory table structure.</summary>
internal static class DocumentStructureValidator
{
    internal static void Validate(AprDocument document, ValidationResult result)
    {
        if (document.Sections is null || document.Sections.Count == 0) { result.AddError(new ValidationError("Document must have at least one section", "sections", "REQUIRED_FIELD")); return; }
        var sectionIds = new HashSet<string>(); var promptIds = new HashSet<string>();
        for (var index = 0; index < document.Sections.Count; index++) ValidateSection(document.Sections[index], $"sections[{index}]", sectionIds, promptIds, result);
    }

    private static void ValidateSection(Section section, string path, HashSet<string> sectionIds, HashSet<string> promptIds, ValidationResult result)
    {
        ValidateIdentifier(section.Id, sectionIds, "Section", $"{path}.id", result);
        if (string.IsNullOrWhiteSpace(section.Title)) result.AddError(new ValidationError("Section title is required", $"{path}.title", "REQUIRED_FIELD"));
        if ((section.Prompts?.Count ?? 0) == 0 && (section.Sections?.Count ?? 0) == 0) result.AddError(new ValidationError("Section must have at least one prompt or child section", path, "EMPTY_SECTION"));
        ValidateTable(section, path, result);
        if (section.Sections is not null) for (var index = 0; index < section.Sections.Count; index++) ValidateSection(section.Sections[index], $"{path}.sections[{index}]", sectionIds, promptIds, result);
        if (section.Prompts is not null) for (var index = 0; index < section.Prompts.Count; index++) ValidatePrompt(section.Prompts[index], $"{path}.prompts[{index}]", promptIds, result);
    }

    private static void ValidateIdentifier(string value, HashSet<string> ids, string subject, string path, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(value)) result.AddError(new ValidationError($"{subject} ID is required", path, "REQUIRED_FIELD"));
        else if (!ids.Add(value)) result.AddError(new ValidationError($"Duplicate {subject.ToLowerInvariant()} ID: {value}", path, "DUPLICATE_ID"));
    }

    private static void ValidatePrompt(Prompt prompt, string path, HashSet<string> promptIds, ValidationResult result)
    {
        ValidateIdentifier(prompt.Id, promptIds, "Prompt", $"{path}.id", result);
        if (string.IsNullOrWhiteSpace(prompt.Label)) result.AddError(new ValidationError("Prompt label is required", $"{path}.label", "REQUIRED_FIELD"));
    }

    private static void ValidateTable(Section section, string path, ValidationResult result)
    {
        if (!section.IsTable) return;
        var rows = section.Sections ?? [];
        if (rows.Count == 0) { result.AddWarning(new ValidationWarning("A table section has no instances. A table always has at least one row; an empty one cannot describe its own fields.", path, "TABLE_NO_ROWS")); return; }
        var first = rows[0].Prompts ?? [];
        foreach (var row in rows.Skip(1))
        {
            var prompts = row.Prompts ?? [];
            if (prompts.Count != first.Count) { result.AddWarning(new ValidationWarning($"Table instance '{row.Id}' has {prompts.Count} prompts but the first has {first.Count}; corresponding fields cannot be aligned by position.", $"{path}.sections", "TABLE_RAGGED")); continue; }
            for (var index = 0; index < prompts.Count; index++) if (!string.Equals(prompts[index].Label, first[index].Label, StringComparison.Ordinal)) result.AddWarning(new ValidationWarning($"Table instance '{row.Id}' names field {index} '{prompts[index].Label}' but the first instance names it '{first[index].Label}'; corresponding fields should share a label.", $"{path}.sections", "TABLE_LABEL_MISMATCH"));
        }
        if (int.TryParse(section.MaxRows, out var maximum) && maximum > 0 && rows.Count > maximum) result.AddWarning(new ValidationWarning($"Table has {rows.Count} instances, above the advisory maximum of {maximum}.", path, "TABLE_OVER_CAPACITY"));
    }
}
