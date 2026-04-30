using System.Text;

namespace PromptResponse.Core.Validation;

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
/// <remarks>
/// <see cref="IsValid"/> reflects only structural <see cref="Errors"/> — required
/// fields, unique IDs, valid hierarchy. Hint mismatches (data type, table capacity,
/// custom patterns) are reported as advisory <see cref="Warnings"/> and never affect
/// <see cref="IsValid"/>. Any visible text is a valid response in PromptResponse.
/// </remarks>
public class ValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResult"/> class.
    /// </summary>
    public ValidationResult()
    {
        Errors = new List<ValidationError>();
        Warnings = new List<ValidationWarning>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResult"/> class with errors.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    private ValidationResult(IEnumerable<ValidationError> errors)
    {
        Errors = new List<ValidationError>(errors);
        Warnings = new List<ValidationWarning>();
    }

    /// <summary>
    /// Gets a value indicating whether the validation succeeded.
    /// </summary>
    /// <remarks>
    /// True when there are no <see cref="Errors"/>. Warnings do not affect this.
    /// </remarks>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets a value indicating whether any advisory warnings were produced.
    /// </summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>
    /// Gets the list of validation errors (structural problems that invalidate the document).
    /// </summary>
    public List<ValidationError> Errors { get; }

    /// <summary>
    /// Gets the list of advisory warnings (hint mismatches; never invalidate the document).
    /// </summary>
    public List<ValidationWarning> Warnings { get; }

    /// <summary>
    /// Creates a valid validation result with no errors.
    /// </summary>
    /// <returns>A valid <see cref="ValidationResult"/>.</returns>
    public static ValidationResult Valid()
    {
        return new ValidationResult();
    }

    /// <summary>
    /// Creates an invalid validation result with a single error.
    /// </summary>
    /// <param name="error">The validation error.</param>
    /// <returns>An invalid <see cref="ValidationResult"/>.</returns>
    public static ValidationResult Invalid(ValidationError error)
    {
        return new ValidationResult(new[] { error });
    }

    /// <summary>
    /// Creates an invalid validation result with multiple errors.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <returns>An invalid <see cref="ValidationResult"/>.</returns>
    public static ValidationResult Invalid(params ValidationError[] errors)
    {
        return new ValidationResult(errors);
    }

    /// <summary>
    /// Adds an error to the validation result.
    /// </summary>
    /// <param name="error">The error to add.</param>
    public void AddError(ValidationError error)
    {
        Errors.Add(error);
    }

    /// <summary>
    /// Adds multiple errors to the validation result.
    /// </summary>
    /// <param name="errors">The errors to add.</param>
    public void AddErrors(IEnumerable<ValidationError> errors)
    {
        Errors.AddRange(errors);
    }

    /// <summary>
    /// Adds an advisory warning. Does not affect <see cref="IsValid"/>.
    /// </summary>
    /// <param name="warning">The warning to add.</param>
    public void AddWarning(ValidationWarning warning)
    {
        Warnings.Add(warning);
    }

    /// <summary>
    /// Adds multiple advisory warnings. Does not affect <see cref="IsValid"/>.
    /// </summary>
    /// <param name="warnings">The warnings to add.</param>
    public void AddWarnings(IEnumerable<ValidationWarning> warnings)
    {
        Warnings.AddRange(warnings);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (IsValid && !HasWarnings)
        {
            return "Validation succeeded";
        }

        var sb = new StringBuilder();
        if (!IsValid)
        {
            sb.AppendLine($"Validation failed with {Errors.Count} error(s):");
            foreach (var error in Errors)
            {
                sb.AppendLine($"  - {error}");
            }
        }
        if (HasWarnings)
        {
            if (!IsValid)
            {
                sb.AppendLine();
            }
            sb.AppendLine($"With {Warnings.Count} advisory warning(s):");
            foreach (var warning in Warnings)
            {
                sb.AppendLine($"  - {warning}");
            }
        }
        return sb.ToString();
    }
}
