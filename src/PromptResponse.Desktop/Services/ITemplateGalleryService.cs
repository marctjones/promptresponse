using PromptResponse.Core.Models;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Represents a template in the gallery with metadata.
/// </summary>
public record TemplateGalleryItem(
    string Key,
    string TemplateId,
    string? Title,
    string? Description,
    string? Author,
    string? Version,
    bool IsSigned,
    long Size,
    DateTime LastModified);

/// <summary>
/// Service for browsing and downloading templates from S3 template galleries.
/// </summary>
public interface ITemplateGalleryService
{
    /// <summary>
    /// Lists available templates in a template gallery (S3 bucket).
    /// </summary>
    /// <param name="config">S3 bucket configuration for the gallery.</param>
    /// <param name="prefix">Optional prefix to filter templates (e.g., "templates/official/").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of templates with metadata.</returns>
    Task<IReadOnlyList<TemplateGalleryItem>> BrowseTemplatesAsync(
        S3BucketConfig config,
        string? prefix = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a template from the gallery.
    /// </summary>
    /// <param name="config">S3 bucket configuration for the gallery.</param>
    /// <param name="key">The S3 object key of the template.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The downloaded template document.</returns>
    Task<AprDocument> DownloadTemplateAsync(
        S3BucketConfig config,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads and verifies a signed template from the gallery.
    /// </summary>
    /// <param name="config">S3 bucket configuration for the gallery.</param>
    /// <param name="key">The S3 object key of the template.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple with (template, verificationResult).</returns>
    Task<(AprDocument Template, SignatureVerificationResult? VerificationResult)> DownloadAndVerifyTemplateAsync(
        S3BucketConfig config,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches templates by keyword in title, description, or template ID.
    /// </summary>
    /// <param name="config">S3 bucket configuration for the gallery.</param>
    /// <param name="searchTerm">The search keyword.</param>
    /// <param name="prefix">Optional prefix to filter search.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching templates.</returns>
    Task<IReadOnlyList<TemplateGalleryItem>> SearchTemplatesAsync(
        S3BucketConfig config,
        string searchTerm,
        string? prefix = null,
        CancellationToken cancellationToken = default);
}
