namespace PromptResponse.Core.Expressions;

/// <summary>
/// Interface for providing field values during expression evaluation.
/// </summary>
public interface IEvaluationContext
{
    /// <summary>
    /// Gets the value of a field by its ID.
    /// </summary>
    /// <param name="fieldId">The field identifier.</param>
    /// <returns>The field value as a string, or empty string if not found.</returns>
    string GetFieldValue(string fieldId);

    /// <summary>
    /// Checks if a field exists in the context.
    /// </summary>
    /// <param name="fieldId">The field identifier.</param>
    /// <returns>True if the field exists, false otherwise.</returns>
    bool HasField(string fieldId);
}
