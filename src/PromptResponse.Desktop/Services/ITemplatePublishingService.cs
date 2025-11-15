using PromptResponse.Core.Models;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Service for publishing signed templates to S3 for distribution.
/// </summary>
public interface ITemplatePublishingService
{
    /// <summary>
    /// Publishes a signed template to S3 for distribution.
    /// </summary>
    /// <param name="config">S3 bucket configuration.</param>
    /// <param name="template">The template document to publish.</param>
    /// <param name="fileName">Optional custom filename. If null, uses templateId.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The S3 object key where the template was published.</returns>
    /// <exception cref="ArgumentNullException">If config or template is null.</exception>
    /// <exception cref="InvalidOperationException">If template is not signed or is not a template.</exception>
    /// <exception cref="InvalidOperationException">If template fails validation.</exception>
    Task<string> PublishTemplateAsync(
        S3BucketConfig config,
        AprDocument template,
        string? fileName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a document can be published as a template.
    /// </summary>
    /// <param name="document">The document to validate.</param>
    /// <returns>A tuple with (isValid, errorMessage). If valid, errorMessage is null.</returns>
    (bool IsValid, string? ErrorMessage) ValidateForPublishing(AprDocument document);

    /// <summary>
    /// Lists published templates in an S3 bucket.
    /// </summary>
    /// <param name="config">S3 bucket configuration.</param>
    /// <param name="prefix">Optional prefix to filter templates (e.g., "templates/").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of published template objects.</returns>
    Task<IReadOnlyList<S3Object>> ListPublishedTemplatesAsync(
        S3BucketConfig config,
        string? prefix = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unpublishes (deletes) a template from S3.
    /// </summary>
    /// <param name="config">S3 bucket configuration.</param>
    /// <param name="key">The S3 object key to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UnpublishTemplateAsync(
        S3BucketConfig config,
        string key,
        CancellationToken cancellationToken = default);
}
