using PromptResponse.Core.Models;

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
            var (patternMatches, patternProblem) = DataTypeHintRules.MatchesPattern(prompt.Response, prompt.Hints.ValidationPattern);
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
        var matchesHint = DataTypeHintRules.Matches(expectedType, prompt.Response);

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
        return DataTypeHintRules.Infer(response);
    }
}
