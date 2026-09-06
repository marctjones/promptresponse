using System.Text.Json;
using System.Text.Json.Serialization;
using PromptResponse.Core.Serialization;
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
    /// Members present in the source JSON that this build does not recognise.
    /// </summary>
    /// <remarks>
    /// Captured on read and written back out unchanged, so a document produced by a
    /// newer minor version of the format survives a round-trip through this build
    /// instead of being silently stripped. This is what makes additive format change
    /// possible; see <see cref="AprFormat"/>.
    ///
    /// Not covered by signatures — the canonical payload enumerates known fields only,
    /// so extension members on a signed document can be altered without invalidating
    /// the signature. Not sanitised either: the text rules apply to known string fields.
    /// </remarks>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }

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

    // ── Bounds hints (specification 4.7) ──
    // Advisory like every other hint: they describe the control to offer,
    // never a rule the response must satisfy.

    /// <summary>
    /// Suggested lower bound for an ordered field, as a string.
    /// </summary>
    /// <remarks>
    /// An offer, not a limit (specification 4.7). A slider that starts at 0 does not make
    /// "-5" an invalid response, and a validator must never reject one. On date, time and
    /// datetime this is the earliest suggested value.
    ///
    /// A native JSON value, not a string. beta.6 reversed the earlier rule here: only a
    /// response is always a string, because only a response is what a person typed.
    /// Everything structural carries the JSON type it means, so a bound on a number is a
    /// number and a bound on a date is the canonical-form string that date is written as
    /// (specification 5.7).
    /// </remarks>
    [JsonConverter(typeof(NumberOrStringConverter))]
    public object? Min { get; set; }

    /// <summary>Suggested upper bound for an ordered field. See <see cref="Min"/>.</summary>
    [JsonConverter(typeof(NumberOrStringConverter))]
    public object? Max { get; set; }

    /// <summary>`min` read as a number, or null when it is not one.</summary>
    /// <remarks>
    /// A bound is a number on `number`, `currency` and `range`, and a canonical-form
    /// string on `date`, `time` and `datetime`. Both are the format's own spellings, so
    /// a reader takes whichever the field's space needs rather than converting between
    /// them: rewriting 5 as "5" would change the document's digest.
    /// </remarks>
    [JsonIgnore]
    public double? MinNumber => AsNumber(Min);

    /// <summary>`max` read as a number, or null when it is not one.</summary>
    [JsonIgnore]
    public double? MaxNumber => AsNumber(Max);

    /// <summary>`min` read as text, whatever spelling it carries.</summary>
    [JsonIgnore]
    public string? MinText => Min?.ToString();

    /// <summary>`max` read as text, whatever spelling it carries.</summary>
    [JsonIgnore]
    public string? MaxText => Max?.ToString();

    private static double? AsNumber(object? value) => value switch
    {
        null => null,
        long whole => whole,
        int whole => whole,
        double number => number,
        decimal number => (double)number,
        string text when double.TryParse(text, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null,
    };

    /// <summary>Suggested increment for an ordered field, as a number.</summary>
    /// <remarks>Always a number: an increment on a date is still a count.</remarks>
    public double? Step { get; set; }

    // ── Expression hints (CEL; specification 8) ──
    // Not a "CEL subset" and not defined in the retired v0.2 appendices: these are CEL,
    // whose grammar and type rules come from cel-spec. All are advisory and pure data -
    // they change what is shown, never what may be entered, and execute no code.
    // A computed value stays editable (specification 8.6).

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
