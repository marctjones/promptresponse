using System.Globalization;

namespace PromptResponse.Desktop.InputFormatters;

/// <summary>
/// Currency input formatter: when the input parses as a decimal, reshapes it
/// with the active culture's currency symbol and thousands separators
/// ("$1,234.56"). Free text ("varies", "see notes") passes through unchanged.
/// </summary>
/// <remarks>
/// Currency masks must be commit-time, not per-keystroke: live reshaping while
/// the user types decimals (e.g. "12." → "$12.00") interferes with intent. The
/// surrounding view should invoke this formatter on TextBox.LostFocus, not on
/// every TextChanged event. The contract is still per-keystroke-safe — running
/// it on every keystroke is non-destructive — but UX-wise it's distracting.
/// </remarks>
public sealed class CurrencyInputFormatter : IInputFormatter
{
    private readonly CultureInfo _culture;

    public CurrencyInputFormatter() : this(CultureInfo.CurrentCulture) { }

    public CurrencyInputFormatter(CultureInfo culture)
    {
        _culture = culture ?? throw new ArgumentNullException(nameof(culture));
    }

    public Type GateProfile => typeof(Profiles.CurrencyInputMaskProfile);

    public FormatResult Format(string raw, int caretIndex)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new FormatResult(raw, caretIndex);

        var stripped = raw.Trim().TrimStart(_culture.NumberFormat.CurrencySymbol.ToCharArray()).Trim();
        if (!decimal.TryParse(stripped, NumberStyles.Currency | NumberStyles.AllowThousands,
                _culture, out var value)
            && !decimal.TryParse(stripped, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out value))
        {
            return new FormatResult(raw, caretIndex);
        }

        var formatted = value.ToString("C2", _culture);
        if (formatted == raw) return new FormatResult(raw, caretIndex);

        // Caret moves to end on commit-time format — currency doesn't have a
        // useful per-digit anchor (the symbol position varies by culture).
        return new FormatResult(formatted, formatted.Length);
    }
}
