using static PromptResponse.Desktop.InputFormatters.InputFormatterUtilities;

namespace PromptResponse.Desktop.InputFormatters;

/// <summary>
/// US ZIP-code formatter: leaves 5-digit codes untouched ("90210") and shapes
/// 9-digit ZIP+4 codes as "#####-####". Free-text passes through (so foreign
/// postal codes like "EC1A 1BB" or "M5V 3M8" stay as the user typed them).
/// </summary>
public sealed class ZipCodeInputFormatter : IInputFormatter
{
    private static readonly char[] AllowedDelimiters = { ' ', '-' };

    public Type GateProfile => typeof(Profiles.ZipInputMaskProfile);

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
        var formatted = digits.Length <= 5 ? digits : $"{digits[..5]}-{digits[5..]}";

        if (formatted == raw) return new FormatResult(raw, caretIndex);

        var newCaret = CaretAfterDigit(formatted, digitsBefore);
        return new FormatResult(formatted, newCaret);
    }
}
