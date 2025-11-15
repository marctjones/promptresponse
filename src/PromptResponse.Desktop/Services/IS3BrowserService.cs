using PromptResponse.Core.Models;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Represents an S3 object in a bucket.
/// </summary>
public record S3Object(
    string Key,
    long Size,
    DateTime LastModified,
    string? ETag);

/// <summary>
/// Configuration for connecting to an S3 bucket.
/// </summary>
public class S3BucketConfig
{
    /// <summary>
    /// Gets or sets the S3 service endpoint URL.
    /// </summary>
    /// <example>
    /// "https://s3.us-east-1.amazonaws.com" or "http://localhost:9000" for MinIO
    /// </example>
    public string ServiceUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bucket name.
    /// </summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the AWS access key ID.
    /// </summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the AWS secret access key.
    /// </summary>
    public string SecretAccessKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the AWS region.
    /// </summary>
    /// <remarks>
    /// For MinIO, use "us-east-1" (default).
    /// </remarks>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Gets or sets whether to use path-style access.
    /// </summary>
    /// <remarks>
    /// Set to true for MinIO and local testing. AWS S3 supports both styles.
    /// </remarks>
    public bool ForcePathStyle { get; set; } = true;
}

/// <summary>
/// Service for browsing and downloading files from S3 buckets.
/// </summary>
public interface IS3BrowserService
{
    /// <summary>
    /// Lists objects in an S3 bucket.
    /// </summary>
    /// <param name="config">S3 bucket configuration.</param>
    /// <param name="prefix">Optional prefix to filter objects (e.g., "filled-forms/").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of S3 objects.</returns>
    Task<IReadOnlyList<S3Object>> ListObjectsAsync(
        S3BucketConfig config,
        string? prefix = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads an object from S3 and deserializes it as an APR document.
    /// </summary>
    /// <param name="config">S3 bucket configuration.</param>
    /// <param name="key">The S3 object key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized APR document.</returns>
    Task<AprDocument> DownloadDocumentAsync(
        S3BucketConfig config,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests connection to an S3 bucket.
    /// </summary>
    /// <param name="config">S3 bucket configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if connection successful, false otherwise.</returns>
    Task<bool> TestConnectionAsync(
        S3BucketConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an object from S3.
    /// </summary>
    /// <param name="config">S3 bucket configuration.</param>
    /// <param name="key">The S3 object key to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteObjectAsync(
        S3BucketConfig config,
        string key,
        CancellationToken cancellationToken = default);
}
