namespace PromptResponse.Core.Models;

/// <summary>
/// Hints and suggestions for how a prompt should be presented and validated.
/// </summary>
/// <remarks>
/// All hints are advisory only and should never prevent a user from entering
/// any valid text string as a response. These hints help the UI provide a
/// better user experience but do not enforce restrictions.
/// </remarks>
public class PromptHints
{
    /// <summary>
    /// Gets or sets placeholder text shown in an empty input field.
    /// </summary>
    /// <example>
    /// "Enter your email address" or "YYYY-MM-DD"
    /// </example>
    public string? Placeholder { get; set; }

    /// <summary>
    /// Gets or sets the expected data type for this prompt.
    /// </summary>
    /// <remarks>
    /// Common values: "text", "email", "phone", "date", "time", "datetime",
    /// "number", "url", "multiline", "currency", "boolean".
    /// The UI may use this to show appropriate input widgets (e.g., date picker),
    /// but any text string is always allowed as a response.
    /// </remarks>
    public string? ExpectedDataType { get; set; }

    /// <summary>
    /// Gets or sets a list of suggested values for autocomplete.
    /// </summary>
    /// <remarks>
    /// The UI may show these as autocomplete suggestions, but users can
    /// always enter their own value.
    /// </remarks>
    public List<string> SuggestedValues { get; set; } = new();

    /// <summary>
    /// Gets or sets help text providing additional guidance to the user.
    /// </summary>
    /// <example>
    /// "Enter your name as it appears on government ID"
    /// </example>
    public string? HelpText { get; set; }

    /// <summary>
    /// Gets or sets an optional regex pattern for validation.
    /// </summary>
    /// <remarks>
    /// This is an advisory hint only. The UI may show validation feedback,
    /// but should not prevent the user from entering non-matching text.
    /// </remarks>
    /// <example>
    /// @"^\d{4}-\d{2}-\d{2}$" for ISO date format
    /// </example>
    public string? ValidationPattern { get; set; }
}
