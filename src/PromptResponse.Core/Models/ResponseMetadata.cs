namespace PromptResponse.Core.Models;

/// <summary>
/// Metadata about a prompt response in a filled form.
/// </summary>
/// <remarks>
/// This metadata is optional and primarily used in filled forms to track
/// information about how and when the response was provided.
/// </remarks>
public class ResponseMetadata
{
    /// <summary>
    /// Gets or sets the inferred or detected data type of the response.
    /// </summary>
    /// <remarks>
    /// This is an optional hint about what type of data was detected in the response.
    /// Common values: "text", "email", "date", "number", etc.
    /// </remarks>
    public string? InferredDataType { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the response was last modified.
    /// </summary>
    /// <remarks>
    /// Automatically updated when the response value changes.
    /// Stored as UTC timestamp.
    /// </remarks>
    public DateTime? LastModified { get; set; }
}
