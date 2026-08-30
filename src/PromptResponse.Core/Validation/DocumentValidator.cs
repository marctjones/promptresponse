using PromptResponse.Core.Models;

namespace PromptResponse.Core.Validation;

/// <summary>Assembles APR structural validation results from independent rule boundaries.</summary>
public class DocumentValidator : IValidator<AprDocument>
{
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
        switch (AprFormat.Classify(document.Version))
        {
            case VersionCompatibility.Unparseable:
            case VersionCompatibility.UnsupportedMajor:
                result.AddError(new ValidationError($"Unsupported version '{document.Version}'. Supported: {AprFormat.SupportedVersionsDescription}", "version", "UNSUPPORTED_VERSION")); break;
            case VersionCompatibility.NewerMinor:
                result.AddWarning(new ValidationWarning($"Document declares version '{document.Version}', newer than this build understands ({AprFormat.KnownMajor}.{AprFormat.KnownMinor}). It will be read, and members that are not recognised are preserved unchanged.", "version", "NEWER_MINOR_VERSION")); break;
        }
    }

    private static void ValidateMetadata(AprDocument document, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(document.Metadata.Title)) result.AddError(new ValidationError("Title is required", "metadata.title", "REQUIRED_FIELD"));
        if (document.DocumentType == DocumentType.FilledForm && string.IsNullOrWhiteSpace(document.Metadata.TemplateId)) result.AddError(new ValidationError("FilledForm must have a templateId", "metadata.templateId", "REQUIRED_FIELD"));
    }
}
