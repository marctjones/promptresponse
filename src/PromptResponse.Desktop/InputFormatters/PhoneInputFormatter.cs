using static PromptResponse.Desktop.InputFormatters.InputFormatterUtilities;

namespace PromptResponse.Desktop.InputFormatters;

/// <summary>
/// US-style phone formatter: shapes 10-digit input as "(555) 555-5555" and
/// 11-digit "1"-leading input as "+1 (555) 555-5555". Free-text passes through
/// unchanged so "(see HR)" or "ext. front desk" stays as the user typed it.
/// </summary>
public sealed class PhoneInputFormatter : IInputFormatter
{
    private static readonly char[] AllowedDelimiters = { ' ', '(', ')', '-', '.', '+' };

    public Type GateProfile => typeof(Profiles.PhoneInputMaskProfile);

    public FormatResult Format(string raw, int caretIndex)
    {
        if (!LooksLikeStructured(raw, AllowedDelimiters))
        {
            return new FormatResult(raw, caretIndex);
        }

        var digits = ExtractDigits(raw);
        if (digits.Length == 0) return new FormatResult(raw, caretIndex);

        // Cap at 11 digits ("1" + 10). Anything longer is more likely an extension
        // or accidental paste — pass through unchanged rather than truncate visibly.
        if (digits.Length > 11) return new FormatResult(raw, caretIndex);

        var digitsBefore = CountDigitsBefore(raw, caretIndex);
        var formatted = Shape(digits);

        // If the formatter didn't change anything, preserve the original caret.
        if (formatted == raw) return new FormatResult(raw, caretIndex);

        var newCaret = CaretAfterDigit(formatted, digitsBefore);
        return new FormatResult(formatted, newCaret);
    }

    private static string Shape(string digits)
    {
        return digits.Length switch
        {
            <= 3 => $"({digits}",
            <= 6 => $"({digits[..3]}) {digits[3..]}",
            <= 10 => $"({digits[..3]}) {digits[3..6]}-{digits[6..]}",
            11 => $"+{digits[..1]} ({digits[1..4]}) {digits[4..7]}-{digits[7..]}",
            _ => digits,
        };
    }
}
