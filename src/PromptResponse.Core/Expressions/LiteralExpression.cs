namespace PromptResponse.Core.Expressions;

/// <summary>
/// Represents a literal value expression (number or string).
/// </summary>
public class LiteralExpression : IExpression
{
    private readonly object _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiteralExpression"/> class.
    /// </summary>
    /// <param name="value">The literal value.</param>
    public LiteralExpression(object value)
    {
        _value = value;
    }

    /// <inheritdoc/>
    public object Evaluate(IEvaluationContext context)
    {
        return _value;
    }

    /// <inheritdoc/>
    public override string ToString() => _value?.ToString() ?? "null";
}
