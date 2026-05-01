using static PromptResponse.Desktop.InputFormatters.InputFormatterUtilities;

namespace PromptResponse.Desktop.InputFormatters;

/// <summary>
/// US Employer Identification Number formatter: shapes 9-digit input as "##-#######".
/// Free-text passes through.
/// </summary>
public sealed class EinInputFormatter : IInputFormatter
{
    private static readonly char[] AllowedDelimiters = { ' ', '-' };

    public Type GateProfile => typeof(Profiles.EinInputMaskProfile);

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
        var formatted = digits.Length <= 2 ? digits : $"{digits[..2]}-{digits[2..]}";

        if (formatted == raw) return new FormatResult(raw, caretIndex);

        var newCaret = CaretAfterDigit(formatted, digitsBefore);
        return new FormatResult(formatted, newCaret);
    }
}
