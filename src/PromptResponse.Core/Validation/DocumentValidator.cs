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
    private const string SupportedVersion = "1.0";

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
        else if (document.Version != SupportedVersion)
        {
            result.AddError(new ValidationError($"Unsupported version '{document.Version}'. Supported version: {SupportedVersion}", "version", "UNSUPPORTED_VERSION"));
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

        // Section must have at least one prompt or child section
        var hasContent = (section.Prompts != null && section.Prompts.Count > 0) ||
                       (section.Sections != null && section.Sections.Count > 0);
        if (!hasContent)
        {
            result.AddError(new ValidationError("Section must have at least one prompt or child section", path, "EMPTY_SECTION"));
        }

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
