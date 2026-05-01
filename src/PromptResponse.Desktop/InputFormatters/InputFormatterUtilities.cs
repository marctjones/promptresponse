namespace PromptResponse.Desktop.InputFormatters;

/// <summary>
/// Shared helpers for digit-anchored input formatters. All masks share the same
/// caret-tracking trick: count digits before the caret in the raw input, format
/// the digits, then walk the formatted string to find where that many digits
/// have appeared — the caret lands just after that position.
/// </summary>
internal static class InputFormatterUtilities
{
    /// <summary>True if <paramref name="raw"/> contains only the characters this set
    /// of formatters expects (digits + standard delimiters). When false, the formatter
    /// must pass the input through unchanged because the user is typing free text.</summary>
    public static bool LooksLikeStructured(string raw, ReadOnlySpan<char> allowedDelimiters)
    {
        if (string.IsNullOrEmpty(raw)) return false;
        var hasDigit = false;
        foreach (var c in raw)
        {
            if (char.IsDigit(c)) { hasDigit = true; continue; }
            if (allowedDelimiters.IndexOf(c) >= 0) continue;
            return false;
        }
        return hasDigit;
    }

    /// <summary>Counts digits in <paramref name="raw"/>[0..<paramref name="caretIndex"/>).</summary>
    public static int CountDigitsBefore(string raw, int caretIndex)
    {
        if (string.IsNullOrEmpty(raw)) return 0;
        var bound = Math.Min(caretIndex, raw.Length);
        var n = 0;
        for (var i = 0; i < bound; i++)
        {
            if (char.IsDigit(raw[i])) n++;
        }
        return n;
    }

    /// <summary>Walks <paramref name="formatted"/> and returns the index just after
    /// the <paramref name="targetDigitOrdinal"/>'th digit. If the formatted string
    /// has fewer digits, returns the end of the string.</summary>
    public static int CaretAfterDigit(string formatted, int targetDigitOrdinal)
    {
        if (targetDigitOrdinal <= 0) return 0;
        var seen = 0;
        for (var i = 0; i < formatted.Length; i++)
        {
            if (char.IsDigit(formatted[i]))
            {
                seen++;
                if (seen == targetDigitOrdinal) return i + 1;
            }
        }
        return formatted.Length;
    }

    /// <summary>Extracts only the digit characters from <paramref name="raw"/>.</summary>
    public static string ExtractDigits(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        var buf = new System.Text.StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            if (char.IsDigit(c)) buf.Append(c);
        }
        return buf.ToString();
    }
}
