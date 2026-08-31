using PromptResponse.Core.Models;

namespace PromptResponse.Core.Validation;

/// <summary>Assembles APR structural validation results from independent rule boundaries.</summary>
public class DocumentValidator : IValidator<AprDocument>
{
    /// <summary>Validates an APR document and returns every structural error and advisory.</summary>
    public ValidationResult Validate(AprDocument? document)
    {
        var result = new ValidationResult();
        if (document is null) { result.AddError(new ValidationError("Document cannot be null", "document", "NULL_DOCUMENT")); return result; }
        ValidateVersion(document, result);
        ValidateMetadata(document, result);
        DocumentStructureValidator.Validate(document, result);
        return result;
    }

    private static void ValidateVersion(AprDocument document, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(document.Version)) { result.AddError(new ValidationError("Version is required", "version", "REQUIRED_FIELD")); return; }
        if (!AprFormat.IsSupported(document.Version))
            result.AddError(new ValidationError($"Unsupported APR version '{document.Version}'. This build accepts only {AprFormat.CurrentVersion}.", "version", "UNSUPPORTED_VERSION"));
    }

    private static void ValidateMetadata(AprDocument document, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(document.Metadata.Title)) result.AddError(new ValidationError("Title is required", "metadata.title", "REQUIRED_FIELD"));
        if (document.DocumentType == DocumentType.FilledForm && string.IsNullOrWhiteSpace(document.Metadata.TemplateId)) result.AddError(new ValidationError("FilledForm must have a templateId", "metadata.templateId", "REQUIRED_FIELD"));
    }
}
