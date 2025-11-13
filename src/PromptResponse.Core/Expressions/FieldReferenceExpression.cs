namespace PromptResponse.Core.Expressions;

/// <summary>
/// Represents a reference to a field value.
/// </summary>
public class FieldReferenceExpression : IExpression
{
    private readonly string _fieldId;

    /// <summary>
    /// Gets the field identifier.
    /// </summary>
    public string FieldId => _fieldId;

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldReferenceExpression"/> class.
    /// </summary>
    /// <param name="fieldId">The field identifier to reference.</param>
    public FieldReferenceExpression(string fieldId)
    {
        if (string.IsNullOrWhiteSpace(fieldId))
            throw new ArgumentException("Field ID cannot be empty", nameof(fieldId));

        _fieldId = fieldId;
    }

    /// <inheritdoc/>
    public object Evaluate(IEvaluationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        return context.GetFieldValue(_fieldId);
    }

    /// <inheritdoc/>
    public override string ToString() => $"{{{_fieldId}}}";
}
