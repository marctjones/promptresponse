namespace PromptResponse.Core.Expressions;

/// <summary>
/// Represents a reference to a field value.
/// </summary>
public class FieldReferenceExpression : IExpression
{
    private readonly string _fieldId;

    public string FieldId => _fieldId;

    public FieldReferenceExpression(string fieldId)
    {
        if (string.IsNullOrWhiteSpace(fieldId))
            throw new ArgumentException("Field ID cannot be empty", nameof(fieldId));

        _fieldId = fieldId;
    }

    public object Evaluate(IEvaluationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        return context.GetFieldValue(_fieldId);
    }

    public override string ToString() => $"{{{_fieldId}}}";
}
