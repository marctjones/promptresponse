using PromptResponse.Core.Models;

namespace PromptResponse.Core.Validation;

/// <summary>
/// Validates APR documents for structural correctness.
/// </summary>
/// <remarks>
/// This validator checks:
/// - Required fields are present
/// - IDs are unique within their scope
/// - Sections contain at least one prompt
/// - FilledForms reference a template
/// </remarks>
public class DocumentValidator : IValidator<AprDocument>
{
    /// <inheritdoc />
    public ValidationResult Validate(AprDocument? document)
    {
        var result = new ValidationResult();

        if (document == null)
        {
            result.AddError(new ValidationError("Document cannot be null", "document", "NULL_DOCUMENT"));
            return result;
        }

        // Validate version
        ValidateVersion(document, result);

        // Validate metadata
        ValidateMetadata(document, result);

        // Validate sections
        ValidateSections(document, result);

        return result;
    }

    private void ValidateVersion(AprDocument document, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(document.Version))
        {
            result.AddError(new ValidationError("Version is required", "version", "REQUIRED_FIELD"));
        }
        else
        {
            switch (AprFormat.Classify(document.Version))
            {
                case VersionCompatibility.Unparseable:
                case VersionCompatibility.UnsupportedMajor:
                    result.AddError(new ValidationError(
                        $"Unsupported version '{document.Version}'. Supported: {AprFormat.SupportedVersionsDescription}",
                        "version", "UNSUPPORTED_VERSION"));
                    break;

                // A newer minor is readable, not an error: the document may carry members
                // this build does not understand, which are ignored but preserved on write.
                case VersionCompatibility.NewerMinor:
                    result.AddWarning(new ValidationWarning(
                        $"Document declares version '{document.Version}', newer than this build understands ({AprFormat.KnownMajor}.{AprFormat.KnownMinor}). It will be read, and members that are not recognised are preserved unchanged.",
                        "version", "NEWER_MINOR_VERSION"));
                    break;
            }
        }
    }

    private void ValidateMetadata(AprDocument document, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(document.Metadata.Title))
        {
            result.AddError(new ValidationError("Title is required", "metadata.title", "REQUIRED_FIELD"));
        }

        // FilledForms must reference a template
        if (document.DocumentType == DocumentType.FilledForm)
        {
            if (string.IsNullOrWhiteSpace(document.Metadata.TemplateId))
            {
                result.AddError(new ValidationError("FilledForm must have a templateId", "metadata.templateId", "REQUIRED_FIELD"));
            }
        }
    }

    private void ValidateSections(AprDocument document, ValidationResult result)
    {
        if (document.Sections == null || document.Sections.Count == 0)
        {
            result.AddError(new ValidationError("Document must have at least one section", "sections", "REQUIRED_FIELD"));
            return;
        }

        var allSectionIds = new HashSet<string>();
        var allPromptIds = new HashSet<string>();

        for (int i = 0; i < document.Sections.Count; i++)
        {
            ValidateSection(document.Sections[i], $"sections[{i}]", allSectionIds, allPromptIds, result);
        }
    }

    private void ValidateSection(Section section, string path, HashSet<string> allSectionIds, HashSet<string> allPromptIds, ValidationResult result)
    {
        // Validate section ID
        if (string.IsNullOrWhiteSpace(section.Id))
        {
            result.AddError(new ValidationError("Section ID is required", $"{path}.id", "REQUIRED_FIELD"));
        }
        else if (allSectionIds.Contains(section.Id))
        {
            result.AddError(new ValidationError($"Duplicate section ID: {section.Id}", $"{path}.id", "DUPLICATE_ID"));
        }
        else
        {
            allSectionIds.Add(section.Id);
        }

        // Validate section title
        if (string.IsNullOrWhiteSpace(section.Title))
        {
            result.AddError(new ValidationError("Section title is required", $"{path}.title", "REQUIRED_FIELD"));
        }

        // Every section must carry content, tables included. A table always has at
        // least one instance: an "empty" table was never empty — a UI offering to add
        // the first row is already presenting a row, so the row belongs in the data
        // and how it is shown is the renderer's business.
        var hasContent = (section.Prompts != null && section.Prompts.Count > 0) ||
                       (section.Sections != null && section.Sections.Count > 0);
        if (!hasContent)
        {
            result.AddError(new ValidationError("Section must have at least one prompt or child section", path, "EMPTY_SECTION"));
        }

        ValidateTable(section, path, result);

        // Validate child sections (recursive)
        if (section.Sections != null)
        {
            for (int i = 0; i < section.Sections.Count; i++)
            {
                ValidateSection(section.Sections[i], $"{path}.sections[{i}]", allSectionIds, allPromptIds, result);
            }
        }

        // Validate prompts
        if (section.Prompts != null)
        {
            for (int i = 0; i < section.Prompts.Count; i++)
            {
                ValidatePrompt(section.Prompts[i], $"{path}.prompts[{i}]", allPromptIds, result);
            }
        }
    }

    /// <summary>
    /// Table rules are advisory. A ragged or over-capacity table is still a valid
    /// document — the structure is unusual, not wrong, and refusing to open it would
    /// lose whatever the filler had already written.
    /// </summary>
    private void ValidateTable(Section section, string path, ValidationResult result)
    {
        if (!section.IsTable)
        {
            return;
        }

        var rows = section.Sections ?? [];
        if (rows.Count == 0)
        {
            result.AddWarning(new ValidationWarning(
                "A table section has no instances. A table always has at least one row; an empty one cannot describe its own fields.",
                path, "TABLE_NO_ROWS"));
            return;
        }

        // Prompts at the same position correspond across instances. Disagreement is
        // reported, never rejected.
        var first = rows[0].Prompts ?? [];
        foreach (var row in rows.Skip(1))
        {
            var prompts = row.Prompts ?? [];
            if (prompts.Count != first.Count)
            {
                result.AddWarning(new ValidationWarning(
                    $"Table instance '{row.Id}' has {prompts.Count} prompts but the first has {first.Count}; corresponding fields cannot be aligned by position.",
                    $"{path}.sections", "TABLE_RAGGED"));
                continue;
            }
            for (var i = 0; i < prompts.Count; i++)
            {
                if (!string.Equals(prompts[i].Label, first[i].Label, StringComparison.Ordinal))
                {
                    result.AddWarning(new ValidationWarning(
                        $"Table instance '{row.Id}' names field {i} '{prompts[i].Label}' but the first instance names it '{first[i].Label}'; corresponding fields should share a label.",
                        $"{path}.sections", "TABLE_LABEL_MISMATCH"));
                }
            }
        }

        if (int.TryParse(section.MaxRows, out var maxRows) && maxRows > 0 && rows.Count > maxRows)
        {
            result.AddWarning(new ValidationWarning(
                $"Table has {rows.Count} instances, above the advisory maximum of {maxRows}.",
                path, "TABLE_OVER_CAPACITY"));
        }
    }

    private void ValidatePrompt(Prompt prompt, string path, HashSet<string> allPromptIds, ValidationResult result)
    {
        // Validate prompt ID
        if (string.IsNullOrWhiteSpace(prompt.Id))
        {
            result.AddError(new ValidationError("Prompt ID is required", $"{path}.id", "REQUIRED_FIELD"));
        }
        else if (allPromptIds.Contains(prompt.Id))
        {
            result.AddError(new ValidationError($"Duplicate prompt ID: {prompt.Id}", $"{path}.id", "DUPLICATE_ID"));
        }
        else
        {
            allPromptIds.Add(prompt.Id);
        }

        // Validate prompt label
        if (string.IsNullOrWhiteSpace(prompt.Label))
        {
            result.AddError(new ValidationError("Prompt label is required", $"{path}.label", "REQUIRED_FIELD"));
        }
    }
}
