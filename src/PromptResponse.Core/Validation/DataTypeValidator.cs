using PromptResponse.Core.Models;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PromptResponse.Core.Validation;

/// <summary>
/// Inspects prompt responses against their advisory data-type and pattern hints.
/// </summary>
/// <remarks>
/// This is ADVISORY ONLY. The validator never produces <see cref="ValidationError"/>s:
/// any visible text is a valid response in PromptResponse. Hint mismatches are surfaced
/// as <see cref="ValidationWarning"/>s so UIs and downstream programs can offer helpful
/// feedback without blocking the user.
/// </remarks>
public class DataTypeValidator
{
    /// <summary>
    /// Inspects a prompt response. The result is always <see cref="ValidationResult.IsValid"/> = true;
    /// hint mismatches are added to <see cref="ValidationResult.Warnings"/>.
    /// </summary>
    public ValidationResult ValidateResponse(Prompt prompt)
    {
        var result = new ValidationResult();

        // Empty responses produce no advisories
        if (string.IsNullOrWhiteSpace(prompt.Response))
        {
            return result;
        }

        // Inspect against custom pattern first (if present)
        if (!string.IsNullOrWhiteSpace(prompt.Hints.ValidationPattern))
        {
            var (patternMatches, patternProblem) = ValidatePattern(prompt.Response, prompt.Hints.ValidationPattern);
            if (!patternMatches)
            {
                result.AddWarning(new ValidationWarning(
                    patternProblem ?? "Response does not match the suggested pattern",
                    prompt.Id,
                    "PATTERN_MISMATCH"));
                return result; // Pattern advisory takes precedence
            }
        }

        // No expected type = no further advisory possible
        var expectedType = prompt.Hints.ExpectedDataType;
        if (string.IsNullOrWhiteSpace(expectedType))
        {
            return result;
        }

        // Inspect against known data type hints
        var matchesHint = expectedType.ToLowerInvariant() switch
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
            "table" => ValidateTable(prompt.Response, prompt.Hints.TableDefinition),
            "text" => true, // Always matches
            "multiline" => true, // Always matches
            _ => true // Unknown types: no advisory
        };

        if (!matchesHint)
        {
            result.AddWarning(new ValidationWarning(
                $"Response '{prompt.Response}' does not look like '{expectedType}' (advisory)",
                prompt.Id,
                "TYPE_MISMATCH"));
        }

        return result;
    }

    /// <summary>
    /// Inspects all prompts in a document. Always returns <see cref="ValidationResult.IsValid"/> = true;
    /// hint mismatches are surfaced as warnings.
    /// </summary>
    public ValidationResult ValidateDocument(AprDocument document)
    {
        var result = new ValidationResult();

        foreach (var section in document.Sections)
        {
            ValidatePromptsInSection(section, result);
        }

        return result;
    }

    private void ValidatePromptsInSection(Section section, ValidationResult result)
    {
        foreach (var prompt in section.Prompts)
        {
            var promptResult = ValidateResponse(prompt);
            result.AddWarnings(promptResult.Warnings);
        }

        foreach (var childSection in section.Sections)
        {
            ValidatePromptsInSection(childSection, result);
        }
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

    private bool ValidateTable(string value, TableDefinition? tableDefinition)
    {
        // If no table definition, just check that it's valid JSON
        if (tableDefinition == null)
        {
            return IsValidJson(value);
        }

        try
        {
            using var doc = JsonDocument.Parse(value);
            var root = doc.RootElement;

            if (tableDefinition.IsFixedTable)
            {
                // Fixed table: expect JSON object with row IDs as keys
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                // Each row should be an object with column IDs as keys
                foreach (var rowProperty in root.EnumerateObject())
                {
                    if (rowProperty.Value.ValueKind != JsonValueKind.Object)
                    {
                        return false;
                    }
                }

                return true;
            }
            else if (tableDefinition.IsDynamicTable)
            {
                // Dynamic table: expect JSON array of objects
                if (root.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                // Validate row count constraints
                var rowCount = root.GetArrayLength();
                if (tableDefinition.DynamicRows != null)
                {
                    if (rowCount < tableDefinition.DynamicRows.MinRows ||
                        rowCount > tableDefinition.DynamicRows.MaxRows)
                    {
                        return false;
                    }
                }

                // Each row should be an object
                foreach (var row in root.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Object)
                    {
                        return false;
                    }
                }

                return true;
            }

            // Neither fixed nor dynamic - just valid JSON is fine
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private bool IsValidJson(string value)
    {
        try
        {
            using var doc = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
