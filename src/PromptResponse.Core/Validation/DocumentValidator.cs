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

        var sectionIds = new HashSet<string>();
        var allPromptIds = new HashSet<string>();

        for (int i = 0; i < document.Sections.Count; i++)
        {
            var section = document.Sections[i];
            var sectionPath = $"sections[{i}]";

            // Validate section ID
            if (string.IsNullOrWhiteSpace(section.Id))
            {
                result.AddError(new ValidationError("Section ID is required", $"{sectionPath}.id", "REQUIRED_FIELD"));
            }
            else if (sectionIds.Contains(section.Id))
            {
                result.AddError(new ValidationError($"Duplicate section ID: {section.Id}", $"{sectionPath}.id", "DUPLICATE_ID"));
            }
            else
            {
                sectionIds.Add(section.Id);
            }

            // Validate section title
            if (string.IsNullOrWhiteSpace(section.Title))
            {
                result.AddError(new ValidationError("Section title is required", $"{sectionPath}.title", "REQUIRED_FIELD"));
            }

            // Section must have at least one prompt or subsection
            var hasContent = (section.Prompts != null && section.Prompts.Count > 0) ||
                           (section.Subsections != null && section.Subsections.Count > 0);
            if (!hasContent)
            {
                result.AddError(new ValidationError("Section must have at least one prompt or subsection", sectionPath, "EMPTY_SECTION"));
            }

            // Validate subsections
            if (section.Subsections != null)
            {
                for (int j = 0; j < section.Subsections.Count; j++)
                {
                    ValidateSubsection(section.Subsections[j], $"{sectionPath}.subsections[{j}]", allPromptIds, result);
                }
            }

            // Validate section-level prompts
            if (section.Prompts != null)
            {
                for (int j = 0; j < section.Prompts.Count; j++)
                {
                    ValidatePrompt(section.Prompts[j], $"{sectionPath}.prompts[{j}]", allPromptIds, result);
                }
            }
        }
    }

    private void ValidateSubsection(Subsection subsection, string path, HashSet<string> allPromptIds, ValidationResult result)
    {
        // Validate subsection ID
        if (string.IsNullOrWhiteSpace(subsection.Id))
        {
            result.AddError(new ValidationError("Subsection ID is required", $"{path}.id", "REQUIRED_FIELD"));
        }

        // Validate subsection title
        if (string.IsNullOrWhiteSpace(subsection.Title))
        {
            result.AddError(new ValidationError("Subsection title is required", $"{path}.title", "REQUIRED_FIELD"));
        }

        // Validate prompts
        if (subsection.Prompts != null)
        {
            for (int i = 0; i < subsection.Prompts.Count; i++)
            {
                ValidatePrompt(subsection.Prompts[i], $"{path}.prompts[{i}]", allPromptIds, result);
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
