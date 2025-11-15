using System.Text;

namespace PromptResponse.Core.Validation;

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResult"/> class.
    /// </summary>
    public ValidationResult()
    {
        Errors = new List<ValidationError>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResult"/> class with errors.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    private ValidationResult(IEnumerable<ValidationError> errors)
    {
        Errors = new List<ValidationError>(errors);
    }

    /// <summary>
    /// Gets a value indicating whether the validation succeeded.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets the list of validation errors.
    /// </summary>
    public List<ValidationError> Errors { get; }

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

    /// <inheritdoc />
    public override string ToString()
    {
        if (IsValid)
        {
            return "Validation succeeded";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Validation failed with {Errors.Count} error(s):");
        foreach (var error in Errors)
        {
            sb.AppendLine($"  - {error}");
        }
        return sb.ToString();
    }
}
