using static PromptResponse.Desktop.InputFormatters.InputFormatterUtilities;

namespace PromptResponse.Desktop.InputFormatters;

/// <summary>
/// US Social Security Number formatter: shapes 9-digit input as "###-##-####".
/// Free-text passes through ("on file with HR" stays untouched).
/// </summary>
public sealed class SsnInputFormatter : IInputFormatter
{
    private static readonly char[] AllowedDelimiters = { ' ', '-' };

    public Type GateProfile => typeof(Profiles.SsnInputMaskProfile);

    public FormatResult Format(string raw, int caretIndex)
    {
        if (!LooksLikeStructured(raw, AllowedDelimiters))
        {
            return new FormatResult(raw, caretIndex);
        }

        var digits = ExtractDigits(raw);
        if (digits.Length == 0 || digits.Length > 9)
        {
            return new FormatResult(raw, caretIndex);
        }

        var digitsBefore = CountDigitsBefore(raw, caretIndex);
        var formatted = Shape(digits);

        if (formatted == raw) return new FormatResult(raw, caretIndex);

        var newCaret = CaretAfterDigit(formatted, digitsBefore);
        return new FormatResult(formatted, newCaret);
    }

    private static string Shape(string digits) => digits.Length switch
    {
        <= 3 => digits,
        <= 5 => $"{digits[..3]}-{digits[3..]}",
        _ => $"{digits[..3]}-{digits[3..5]}-{digits[5..]}",
    };
}
