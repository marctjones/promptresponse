using System.Globalization;

namespace PromptResponse.Desktop.InputFormatters;

/// <summary>
/// Percentage input formatter: when the input parses as a number, appends "%".
/// "12.5" becomes "12.5%". Free text passes through. Idempotent: re-running on
/// "12.5%" returns "12.5%" unchanged.
/// </summary>
/// <remarks>
/// Like currency, this is intended for commit-time (LostFocus) application;
/// running on every keystroke would force the user to fight the trailing "%".
/// </remarks>
public sealed class PercentageInputFormatter : IInputFormatter
{
    private readonly CultureInfo _culture;

    public PercentageInputFormatter() : this(CultureInfo.CurrentCulture) { }

    public PercentageInputFormatter(CultureInfo culture)
    {
        _culture = culture ?? throw new ArgumentNullException(nameof(culture));
    }

    public FormatResult Format(string raw, int caretIndex)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new FormatResult(raw, caretIndex);

        var trimmed = raw.Trim();
        var hadSuffix = trimmed.EndsWith('%');
        var bare = hadSuffix ? trimmed.TrimEnd('%').Trim() : trimmed;

        if (!double.TryParse(bare, NumberStyles.Float | NumberStyles.AllowThousands,
                _culture, out var value)
            && !double.TryParse(bare, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out value))
        {
            return new FormatResult(raw, caretIndex);
        }

        // Preserve user's decimal precision if they wrote any (12 -> "12%", 12.5 -> "12.5%").
        var formatted = value % 1 == 0
            ? $"{value.ToString("0", _culture)}%"
            : $"{value.ToString("G", _culture)}%";

        if (formatted == raw) return new FormatResult(raw, caretIndex);

        return new FormatResult(formatted, formatted.Length);
    }
}
