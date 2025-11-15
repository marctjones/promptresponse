using PromptResponse.Core.Models;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Service for submitting filled forms to S3 buckets using pre-signed POST.
/// </summary>
public interface IS3SubmissionService
{
    /// <summary>
    /// Submits a filled form document to S3 using the pre-signed POST configuration.
    /// </summary>
    /// <param name="document">The filled form document to submit.</param>
    /// <param name="fileName">Optional custom filename. If null, generates from template ID or timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The S3 object key where the file was uploaded.</returns>
    /// <exception cref="ArgumentNullException">If document is null.</exception>
    /// <exception cref="InvalidOperationException">If document has no submission config or config is invalid/expired.</exception>
    /// <exception cref="HttpRequestException">If the S3 upload fails.</exception>
    Task<string> SubmitFormAsync(AprDocument document, string? fileName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a document has valid submission configuration.
    /// </summary>
    /// <param name="document">The document to validate.</param>
    /// <returns>True if the document has valid, non-expired submission config.</returns>
    bool CanSubmit(AprDocument document);

    /// <summary>
    /// Gets the expiration status of the submission configuration.
    /// </summary>
    /// <param name="document">The document to check.</param>
    /// <returns>A tuple with (isExpired, timeRemaining). If no config, returns (false, null).</returns>
    (bool IsExpired, TimeSpan? TimeRemaining) GetExpirationStatus(AprDocument document);
}
