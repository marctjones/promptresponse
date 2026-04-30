namespace PromptResponse.Core.Validation;

/// <summary>
/// Represents an advisory validation warning that does not invalidate a document.
/// </summary>
/// <remarks>
/// Warnings are surfaced for hint mismatches (e.g., a response of "five" to a prompt
/// hinting at a "number" type, or a dynamic table with fewer rows than the suggested
/// <c>MinRows</c>). They never make <see cref="ValidationResult.IsValid"/> false —
/// any visible text is a valid response, and capacity hints are advisory.
/// </remarks>
public class ValidationWarning
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationWarning"/> class.
    /// </summary>
    /// <param name="message">The advisory message.</param>
    /// <param name="propertyPath">The path to the property the warning applies to.</param>
    /// <param name="warningCode">Optional code for programmatic handling.</param>
    public ValidationWarning(string message, string propertyPath, string? warningCode = null)
    {
        Message = message;
        PropertyPath = propertyPath;
        WarningCode = warningCode;
    }

    /// <summary>
    /// Gets the advisory message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the path to the property the warning applies to.
    /// </summary>
    public string PropertyPath { get; }

    /// <summary>
    /// Gets the optional warning code for programmatic handling.
    /// </summary>
    /// <example>
    /// "TYPE_MISMATCH", "PATTERN_MISMATCH", "ROW_COUNT_OUT_OF_HINT_RANGE"
    /// </example>
    public string? WarningCode { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var code = WarningCode != null ? $"[{WarningCode}] " : "";
        return $"{code}{PropertyPath}: {Message}";
    }
}
