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
    /// "number", "url", "multiline", "currency", "boolean", "table".
    /// The UI may use this to show appropriate input widgets (e.g., date picker),
    /// but any text string is always allowed as a response.
    /// For "table" type, see TableDefinition for structure configuration.
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

    // ── Expression hints (CEL subset; see docs/APR_SPECIFICATION_v0.2.md Appendix B/C) ──
    // All are advisory and pure-data: they transform display/visibility/validation,
    // never block input, and never execute code. Evaluated by PromptResponse.Core.Expressions.

    /// <summary>
    /// Expression that, when truthy, hides this prompt (conditional visibility).
    /// </summary>
    /// <example>"emp_status == 'Retired' || emp_status == 'Student'"</example>
    public string? ExprHidden { get; set; }

    /// <summary>
    /// Expression for a computed (read-only) value. When set, the field's value
    /// is derived from this expression and recomputed as its dependencies change.
    /// </summary>
    /// <example>"double(qty) * double(price)"</example>
    public string? ExprValue { get; set; }

    /// <summary>
    /// Expression that, when truthy, marks this prompt as expected/required
    /// (advisory only — never blocks submission).
    /// </summary>
    /// <example>"emp_status == 'Employed'"</example>
    public string? ExprExpected { get; set; }

    /// <summary>
    /// Cross-field validation expression returning a message string; an empty
    /// string means "valid". Surfaced as an advisory, never blocking.
    /// </summary>
    /// <example>"timestamp(_this) &gt; timestamp(start) ? '' : 'Must be after start'"</example>
    public string? ExprValidation { get; set; }

    /// <summary>
    /// Expression that, when truthy, makes this prompt read-only.
    /// </summary>
    public string? ExprReadOnly { get; set; }
}
