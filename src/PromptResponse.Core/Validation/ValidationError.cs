namespace PromptResponse.Core.Validation;

/// <summary>
/// Represents a validation error.
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="propertyPath">The path to the property that failed validation.</param>
    /// <param name="errorCode">Optional error code for programmatic handling.</param>
    public ValidationError(string message, string propertyPath, string? errorCode = null)
    {
        Message = message;
        PropertyPath = propertyPath;
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the path to the property that failed validation.
    /// </summary>
    /// <example>
    /// "metadata.title", "sections[0].id", "sections[0].prompts[1].label"
    /// </example>
    public string PropertyPath { get; }

    /// <summary>
    /// Gets the optional error code for programmatic error handling.
    /// </summary>
    /// <example>
    /// "REQUIRED_FIELD", "INVALID_FORMAT", "DUPLICATE_ID", "TYPE_MISMATCH"
    /// </example>
    public string? ErrorCode { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var code = ErrorCode != null ? $"[{ErrorCode}] " : "";
        return $"{code}{PropertyPath}: {Message}";
    }
}
