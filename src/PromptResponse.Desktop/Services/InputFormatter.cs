using System.Text;
using System.Text.RegularExpressions;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Provides automatic input formatting for various data types.
/// </summary>
public static class InputFormatter
{
    /// <summary>
    /// Formats input based on the expected data type.
    /// </summary>
    /// <param name="input">The raw input string.</param>
    /// <param name="expectedDataType">The expected data type (e.g., "phone", "ssn", "currency").</param>
    /// <returns>The formatted string.</returns>
    public static string Format(string? input, string? expectedDataType)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(expectedDataType))
            return input ?? "";

        return expectedDataType.ToLowerInvariant() switch
        {
            "phone" => FormatPhone(input),
            "ssn" => FormatSSN(input),
            "ein" => FormatEIN(input),
            "creditcard" => FormatCreditCard(input),
            "zipcode" => FormatZipCode(input),
            "currency" => FormatCurrency(input),
            _ => input
        };
    }

    /// <summary>
    /// Formats a phone number as (XXX) XXX-XXXX.
    /// </summary>
    public static string FormatPhone(string input)
    {
        // Extract only digits
        var digits = ExtractDigits(input);

        if (digits.Length == 0) return "";
        if (digits.Length <= 3) return digits;
        if (digits.Length <= 6) return $"({digits[..3]}) {digits[3..]}";
        if (digits.Length <= 10) return $"({digits[..3]}) {digits[3..6]}-{digits[6..]}";

        // Handle country code (11+ digits)
        if (digits.Length >= 11)
        {
            var countryCode = digits[..^10];
            var rest = digits[^10..];
            return $"+{countryCode} ({rest[..3]}) {rest[3..6]}-{rest[6..]}";
        }

        return $"({digits[..3]}) {digits[3..6]}-{digits[6..10]}";
    }

    /// <summary>
    /// Formats a Social Security Number as XXX-XX-XXXX.
    /// </summary>
    public static string FormatSSN(string input)
    {
        var digits = ExtractDigits(input);

        if (digits.Length == 0) return "";
        if (digits.Length <= 3) return digits;
        if (digits.Length <= 5) return $"{digits[..3]}-{digits[3..]}";
        if (digits.Length <= 9) return $"{digits[..3]}-{digits[3..5]}-{digits[5..]}";

        return $"{digits[..3]}-{digits[3..5]}-{digits[5..9]}";
    }

    /// <summary>
    /// Formats an Employer Identification Number as XX-XXXXXXX.
    /// </summary>
    public static string FormatEIN(string input)
    {
        var digits = ExtractDigits(input);

        if (digits.Length == 0) return "";
        if (digits.Length <= 2) return digits;
        if (digits.Length <= 9) return $"{digits[..2]}-{digits[2..]}";

        return $"{digits[..2]}-{digits[2..9]}";
    }

    /// <summary>
    /// Formats a credit card number as XXXX XXXX XXXX XXXX.
    /// </summary>
    public static string FormatCreditCard(string input)
    {
        var digits = ExtractDigits(input);

        if (digits.Length == 0) return "";

        var result = new StringBuilder();
        for (int i = 0; i < digits.Length && i < 16; i++)
        {
            if (i > 0 && i % 4 == 0)
                result.Append(' ');
            result.Append(digits[i]);
        }

        return result.ToString();
    }

    /// <summary>
    /// Formats a ZIP code as XXXXX or XXXXX-XXXX.
    /// </summary>
    public static string FormatZipCode(string input)
    {
        var digits = ExtractDigits(input);

        if (digits.Length == 0) return "";
        if (digits.Length <= 5) return digits;
        if (digits.Length <= 9) return $"{digits[..5]}-{digits[5..]}";

        return $"{digits[..5]}-{digits[5..9]}";
    }

    /// <summary>
    /// Formats currency with commas and decimal places.
    /// </summary>
    public static string FormatCurrency(string input)
    {
        // Remove existing formatting but keep decimal point and minus sign
        var cleaned = Regex.Replace(input, @"[^\d.\-]", "");

        if (string.IsNullOrEmpty(cleaned)) return "";

        // Try to parse as decimal
        if (decimal.TryParse(cleaned, out var amount))
        {
            // Format with commas and 2 decimal places
            return amount.ToString("N2");
        }

        return input;
    }

    /// <summary>
    /// Extracts only digit characters from input.
    /// </summary>
    private static string ExtractDigits(string input)
    {
        return Regex.Replace(input, @"\D", "");
    }

    /// <summary>
    /// Gets the maximum length for a given data type (digits only).
    /// </summary>
    public static int? GetMaxDigits(string? expectedDataType)
    {
        return expectedDataType?.ToLowerInvariant() switch
        {
            "phone" => 11, // With country code
            "ssn" => 9,
            "ein" => 9,
            "creditcard" => 16,
            "zipcode" => 9,
            _ => null
        };
    }
}
