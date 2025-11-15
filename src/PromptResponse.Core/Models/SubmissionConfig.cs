using System.Text.Json.Serialization;

namespace PromptResponse.Core.Models;

/// <summary>
/// Configuration for submitting filled forms directly to remote endpoints.
/// </summary>
/// <remarks>
/// Enables templates to specify how filled forms should be submitted,
/// such as direct S3 uploads via pre-signed POST or webhook endpoints.
/// Currently supports S3 pre-signed POST.
/// </remarks>
public class SubmissionConfig
{
    /// <summary>
    /// Gets or sets the submission method type.
    /// </summary>
    /// <remarks>
    /// Currently supports:
    /// - "s3-presigned-post": Direct upload to S3 using pre-signed POST
    /// - "webhook": HTTP POST to custom endpoint (future)
    /// </remarks>
    /// <example>
    /// "s3-presigned-post"
    /// </example>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target URL for submission.
    /// </summary>
    /// <remarks>
    /// For S3: The S3 bucket endpoint URL (e.g., "https://bucket.s3.region.amazonaws.com/" or "http://localhost:9000/")
    /// For webhooks: The webhook endpoint URL
    /// </remarks>
    /// <example>
    /// "https://my-bucket.s3.us-east-1.amazonaws.com/"
    /// </example>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the S3 pre-signed POST fields.
    /// </summary>
    /// <remarks>
    /// Contains the policy, signature, and other fields required for S3 pre-signed POST.
    /// Only used when Type is "s3-presigned-post".
    /// Field keys typically include: key, policy, signature, AWSAccessKeyId, acl, etc.
    /// </remarks>
    [JsonPropertyName("fields")]
    public Dictionary<string, string>? Fields { get; set; }

    /// <summary>
    /// Gets or sets the expiration timestamp for the submission configuration.
    /// </summary>
    /// <remarks>
    /// For S3 pre-signed POST: When the policy expires and uploads will no longer be accepted.
    /// Implementations should check this before attempting submission and warn users.
    /// Should be UTC timestamp.
    /// </remarks>
    /// <example>
    /// DateTime.UtcNow.AddDays(7) for a 7-day expiration
    /// </example>
    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets optional headers to include in the submission request.
    /// </summary>
    /// <remarks>
    /// For webhook submissions: Custom HTTP headers
    /// For S3: Usually not needed (policy handles authorization)
    /// </remarks>
    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Checks if the submission configuration is expired.
    /// </summary>
    /// <returns>True if expired, false otherwise.</returns>
    public bool IsExpired()
    {
        if (ExpiresAt == null)
        {
            return false; // No expiration set
        }

        return DateTime.UtcNow > ExpiresAt.Value;
    }

    /// <summary>
    /// Gets the time remaining until expiration.
    /// </summary>
    /// <returns>TimeSpan until expiration, or null if no expiration set.</returns>
    public TimeSpan? TimeUntilExpiration()
    {
        if (ExpiresAt == null)
        {
            return null;
        }

        var remaining = ExpiresAt.Value - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// Validates the submission configuration.
    /// </summary>
    /// <returns>True if configuration is valid and not expired.</returns>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Type))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(Url))
        {
            return false;
        }

        if (Type == "s3-presigned-post" && (Fields == null || Fields.Count == 0))
        {
            return false;
        }

        if (IsExpired())
        {
            return false;
        }

        return true;
    }
}
