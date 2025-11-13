using PromptResponse.Core.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PromptResponse.Core.Validation;

/// <summary>
/// Validates prompt responses against expected data types.
/// </summary>
/// <remarks>
/// IMPORTANT: This validation is ADVISORY ONLY. The application should never
/// prevent users from entering any text string as a response. These validations
/// are used to provide helpful feedback, but all text input is always acceptable.
/// </remarks>
public class DataTypeValidator
{
    /// <summary>
    /// Validates a prompt response against its expected data type.
    /// </summary>
    /// <param name="prompt">The prompt to validate.</param>
    /// <returns>Validation result with warnings (not errors) if type mismatch.</returns>
    public ValidationResult ValidateResponse(Prompt prompt)
    {
        var result = new ValidationResult();

        // Empty responses are always valid
        if (string.IsNullOrWhiteSpace(prompt.Response))
        {
            return result;
        }

        // Validate against custom pattern first (if present)
        if (!string.IsNullOrWhiteSpace(prompt.Hints.ValidationPattern))
        {
            var (patternValid, errorMessage) = ValidatePattern(prompt.Response, prompt.Hints.ValidationPattern);
            if (!patternValid)
            {
                result.AddError(new ValidationError(
                    errorMessage ?? "Response does not match expected pattern",
                    prompt.Id,
                    "PATTERN_MISMATCH"));
                return result; // Pattern takes precedence
            }
        }

        // No expected type = always valid (if no pattern was specified)
        var expectedType = prompt.Hints.ExpectedDataType;
        if (string.IsNullOrWhiteSpace(expectedType))
        {
            return result;
        }

        // Validate against known data types
        var isValid = expectedType.ToLowerInvariant() switch
        {
            "email" => ValidateEmail(prompt.Response),
            "date" => ValidateDate(prompt.Response),
            "time" => ValidateTime(prompt.Response),
            "datetime" => ValidateDateTime(prompt.Response),
            "number" => ValidateNumber(prompt.Response),
            "url" => ValidateUrl(prompt.Response),
            "phone" => ValidatePhone(prompt.Response),
            "currency" => ValidateCurrency(prompt.Response),
            "boolean" => ValidateBoolean(prompt.Response),
            "text" => true, // Always valid
            "multiline" => true, // Always valid
            _ => true // Unknown types are always valid
        };

        if (!isValid)
        {
            result.AddError(new ValidationError(
                $"Response '{prompt.Response}' does not match expected type '{expectedType}'",
                prompt.Id,
                "TYPE_MISMATCH"));
        }

        return result;
    }

    /// <summary>
    /// Validates all prompts in a document.
    /// </summary>
    /// <param name="document">The document to validate.</param>
    /// <returns>Validation result with all type mismatches.</returns>
    public ValidationResult ValidateDocument(AprDocument document)
    {
        var result = new ValidationResult();

        foreach (var section in document.Sections)
        {
            // Validate section-level prompts
            foreach (var prompt in section.Prompts)
            {
                var promptResult = ValidateResponse(prompt);
                result.AddErrors(promptResult.Errors);
            }

            // Validate subsection prompts
            foreach (var subsection in section.Subsections)
            {
                foreach (var prompt in subsection.Prompts)
                {
                    var promptResult = ValidateResponse(prompt);
                    result.AddErrors(promptResult.Errors);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Infers the data type from a response value.
    /// </summary>
    /// <param name="response">The response to analyze.</param>
    /// <returns>The inferred data type.</returns>
    public string InferDataType(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return "text";
        }

        // Check for multiline first
        if (response.Contains('\n') || response.Contains('\r'))
        {
            return "multiline";
        }

        // Try specific types in order of specificity
        if (ValidateEmail(response)) return "email";
        if (ValidateUrl(response)) return "url";
        if (ValidateDate(response)) return "date";
        if (ValidateDateTime(response)) return "datetime";
        if (ValidateNumber(response)) return "number";
        if (ValidateCurrency(response)) return "currency";
        if (ValidateTime(response)) return "time";
        if (ValidateBoolean(response)) return "boolean";

        return "text";
    }

    private bool ValidateEmail(string value)
    {
        // Reject emails with consecutive dots
        if (value.Contains(".."))
        {
            return false;
        }

        // Simple email validation: local@domain.tld
        // - No spaces
        // - Must have @ symbol
        // - Must have at least one dot after @
        // - No @ or spaces in local and domain parts
        var emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(value, emailPattern);
    }

    private bool ValidateDate(string value)
    {
        // Accept ISO 8601 date format (YYYY-MM-DD)
        return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _);
    }

    private bool ValidateTime(string value)
    {
        // Accept time formats like HH:mm:ss or HH:mm
        return TimeSpan.TryParse(value, out _);
    }

    private bool ValidateDateTime(string value)
    {
        // Accept ISO 8601 datetime
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _);
    }

    private bool ValidateNumber(string value)
    {
        // Reject numbers with commas (not proper decimal separator in invariant culture)
        if (value.Contains(','))
        {
            return false;
        }

        // Try to parse as double
        if (!double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var result))
        {
            return false;
        }

        // Reject NaN and Infinity
        if (double.IsNaN(result) || double.IsInfinity(result))
        {
            return false;
        }

        return true;
    }

    private bool ValidateUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps ||
                uri.Scheme == Uri.UriSchemeFtp);
    }

    private bool ValidatePhone(string value)
    {
        // Lenient phone validation - just check for digits and common separators
        var phonePattern = @"^[\d\s\-\(\)\+\.]+$";
        return Regex.IsMatch(value, phonePattern) && value.Any(char.IsDigit);
    }

    private bool ValidateCurrency(string value)
    {
        // Remove common currency symbols and try to parse as decimal
        var cleaned = value.Replace("$", "").Replace("£", "").Replace("€", "").Replace(",", "").Trim();
        return decimal.TryParse(cleaned, NumberStyles.Currency, CultureInfo.InvariantCulture, out _);
    }

    private bool ValidateBoolean(string value)
    {
        var lower = value.ToLowerInvariant().Trim();
        return lower == "true" || lower == "false" ||
               lower == "yes" || lower == "no" ||
               lower == "1" || lower == "0";
    }

    private (bool isValid, string? errorMessage) ValidatePattern(string value, string pattern)
    {
        try
        {
            var isMatch = Regex.IsMatch(value, pattern);
            return (isMatch, null);
        }
        catch (ArgumentException ex)
        {
            // Invalid regex pattern - report as validation error
            return (false, $"Invalid validation pattern: {ex.Message}");
        }
    }
}
