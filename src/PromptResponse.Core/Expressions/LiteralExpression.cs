namespace PromptResponse.Core.Expressions;

/// <summary>
/// Represents a literal value expression (number or string).
/// </summary>
public class LiteralExpression : IExpression
{
    private readonly object _value;

    public LiteralExpression(object value)
    {
        _value = value;
    }

    public object Evaluate(IEvaluationContext context)
    {
        return _value;
    }

    public override string ToString() => _value?.ToString() ?? "null";
}
