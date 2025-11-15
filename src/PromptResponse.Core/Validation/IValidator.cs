namespace PromptResponse.Core.Validation;

/// <summary>
/// Interface for validators.
/// </summary>
/// <typeparam name="T">The type to validate.</typeparam>
public interface IValidator<T>
{
    /// <summary>
    /// Validates an item.
    /// </summary>
    /// <param name="item">The item to validate.</param>
    /// <returns>The validation result.</returns>
    ValidationResult Validate(T item);
}
