using System.Globalization;
using System.Text.RegularExpressions;

namespace PromptResponse.Core.Validation;

/// <summary>Pure parsing rules for APR's advisory data-type hints.</summary>
internal static class DataTypeHintRules
{
    internal static bool Matches(string expectedType, string value) => expectedType.ToLowerInvariant() switch
    {
        "email" => IsEmail(value), "date" => IsDate(value), "time" => IsTime(value),
        "datetime" => IsDateTime(value), "number" or "range" => IsNumber(value),
        "url" => IsUrl(value), "phone" => IsPhone(value), "currency" => IsCurrency(value),
        "boolean" => IsBoolean(value), "text" or "multiline" => true, _ => true
    };

    internal static string Infer(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return "text";
        if (response.Contains('\n') || response.Contains('\r')) return "multiline";
        if (IsEmail(response)) return "email";
        if (IsUrl(response)) return "url";
        if (IsDate(response)) return "date";
        if (IsDateTime(response)) return "datetime";
        if (IsNumber(response)) return "number";
        if (IsCurrency(response)) return "currency";
        if (IsTime(response)) return "time";
        if (IsBoolean(response)) return "boolean";
        return "text";
    }

    internal static (bool IsValid, string? ErrorMessage) MatchesPattern(string value, string pattern)
    {
        try { return (Regex.IsMatch(value, pattern), null); }
        catch (ArgumentException ex) { return (false, $"Invalid validation pattern: {ex.Message}"); }
    }

    private static bool IsEmail(string value) => !value.Contains("..") && Regex.IsMatch(value, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    private static bool IsDate(string value) => DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    private static bool IsTime(string value) => TimeSpan.TryParse(value, out _);
    private static bool IsDateTime(string value) => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _);
    private static bool IsNumber(string value) => !value.Contains(',') && double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed) && !double.IsNaN(parsed) && !double.IsInfinity(parsed);
    private static bool IsUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFtp);
    private static bool IsPhone(string value) => Regex.IsMatch(value, @"^[\d\s\-\(\)\+\.]+$") && value.Any(char.IsDigit);
    private static bool IsCurrency(string value) => decimal.TryParse(value.Replace("$", "").Replace("£", "").Replace("€", "").Replace(",", "").Trim(), NumberStyles.Currency, CultureInfo.InvariantCulture, out _);
    private static bool IsBoolean(string value) => value.ToLowerInvariant().Trim() is "true" or "false" or "yes" or "no" or "1" or "0";
}
