namespace PromptResponse.Core.Models;

/// <summary>
/// Represents a single prompt (question/field) in an APR document.
/// </summary>
/// <remarks>
/// Prompts are the fundamental unit of data collection in APR documents.
/// Responses are always stored as strings, regardless of the expected data type.
/// This ensures maximum portability and prevents data loss from type coercion.
/// </remarks>
public class Prompt
{
    private string _response = string.Empty;

    /// <summary>
    /// Gets or sets the unique identifier for this prompt.
    /// </summary>
    /// <remarks>
    /// IDs must be unique within the document and should remain stable across versions.
    /// Recommended format: "prompt_001", "prompt_002", etc.
    /// </remarks>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user-visible label for this prompt.
    /// </summary>
    /// <example>
    /// "Full Legal Name", "Email Address", "Date of Birth"
    /// </example>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the response value.
    /// </summary>
    /// <remarks>
    /// Always stored as a string regardless of expected data type.
    /// Setting this property automatically updates ResponseMetadata.LastModified.
    /// Null values are converted to empty strings.
    /// </remarks>
    public string Response
    {
        get => _response;
        set
        {
            _response = value ?? string.Empty;
            ResponseMetadata.LastModified = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Gets or sets hints for how this prompt should be presented and validated.
    /// </summary>
    public PromptHints Hints { get; set; } = new();

    /// <summary>
    /// Gets or sets metadata about the response.
    /// </summary>
    /// <remarks>
    /// Primarily used in filled forms to track response information.
    /// </remarks>
    public ResponseMetadata ResponseMetadata { get; set; } = new();
}
