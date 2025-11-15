namespace PromptResponse.Core.Expressions;

/// <summary>
/// Binary operators supported in expressions.
/// </summary>
public enum BinaryOperator
{
    /// <summary>Addition operator (+)</summary>
    Add,
    /// <summary>Subtraction operator (-)</summary>
    Subtract,
    /// <summary>Multiplication operator (*)</summary>
    Multiply,
    /// <summary>Division operator (/)</summary>
    Divide,
    /// <summary>Equality operator (==)</summary>
    Equal,
    /// <summary>Inequality operator (!=)</summary>
    NotEqual,
    /// <summary>Less than operator (&lt;)</summary>
    LessThan,
    /// <summary>Less than or equal operator (&lt;=)</summary>
    LessThanOrEqual,
    /// <summary>Greater than operator (&gt;)</summary>
    GreaterThan,
    /// <summary>Greater than or equal operator (&gt;=)</summary>
    GreaterThanOrEqual
}

/// <summary>
/// Represents a binary operation expression.
/// </summary>
public class BinaryExpression : IExpression
{
    private readonly IExpression _left;
    private readonly BinaryOperator _operator;
    private readonly IExpression _right;

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryExpression"/> class.
    /// </summary>
    /// <param name="left">The left operand expression.</param>
    /// <param name="op">The binary operator.</param>
    /// <param name="right">The right operand expression.</param>
    public BinaryExpression(IExpression left, BinaryOperator op, IExpression right)
    {
        _left = left ?? throw new ArgumentNullException(nameof(left));
        _operator = op;
        _right = right ?? throw new ArgumentNullException(nameof(right));
    }

    /// <inheritdoc/>
    public object Evaluate(IEvaluationContext context)
    {
        var leftValue = _left.Evaluate(context);
        var rightValue = _right.Evaluate(context);

        return _operator switch
        {
            BinaryOperator.Add => EvaluateAdd(leftValue, rightValue),
            BinaryOperator.Subtract => EvaluateSubtract(leftValue, rightValue),
            BinaryOperator.Multiply => EvaluateMultiply(leftValue, rightValue),
            BinaryOperator.Divide => EvaluateDivide(leftValue, rightValue),
            BinaryOperator.Equal => EvaluateEqual(leftValue, rightValue),
            BinaryOperator.NotEqual => !EvaluateEqual(leftValue, rightValue),
            BinaryOperator.LessThan => EvaluateLessThan(leftValue, rightValue),
            BinaryOperator.LessThanOrEqual => EvaluateLessThanOrEqual(leftValue, rightValue),
            BinaryOperator.GreaterThan => EvaluateGreaterThan(leftValue, rightValue),
            BinaryOperator.GreaterThanOrEqual => EvaluateGreaterThanOrEqual(leftValue, rightValue),
            _ => throw new InvalidOperationException($"Unknown operator: {_operator}")
        };
    }

    private object EvaluateAdd(object left, object right)
    {
        // Try numeric addition first
        if (TryConvertToDouble(left, out var leftNum) && TryConvertToDouble(right, out var rightNum))
        {
            return leftNum + rightNum;
        }

        // Fallback to string concatenation
        return left?.ToString() + right?.ToString();
    }

    private object EvaluateSubtract(object left, object right)
    {
        var leftNum = ConvertToDouble(left);
        var rightNum = ConvertToDouble(right);
        return leftNum - rightNum;
    }

    private object EvaluateMultiply(object left, object right)
    {
        var leftNum = ConvertToDouble(left);
        var rightNum = ConvertToDouble(right);
        return leftNum * rightNum;
    }

    private object EvaluateDivide(object left, object right)
    {
        var leftNum = ConvertToDouble(left);
        var rightNum = ConvertToDouble(right);

        if (Math.Abs(rightNum) < double.Epsilon)
            throw new DivideByZeroException("Division by zero in expression");

        return leftNum / rightNum;
    }

    private bool EvaluateEqual(object left, object right)
    {
        if (left == null && right == null) return true;
        if (left == null || right == null) return false;

        // Try numeric comparison
        if (TryConvertToDouble(left, out var leftNum) && TryConvertToDouble(right, out var rightNum))
        {
            return Math.Abs(leftNum - rightNum) < double.Epsilon;
        }

        // String comparison
        return left.ToString() == right.ToString();
    }

    private bool EvaluateLessThan(object left, object right)
    {
        var leftNum = ConvertToDouble(left);
        var rightNum = ConvertToDouble(right);
        return leftNum < rightNum;
    }

    private bool EvaluateLessThanOrEqual(object left, object right)
    {
        var leftNum = ConvertToDouble(left);
        var rightNum = ConvertToDouble(right);
        return leftNum <= rightNum;
    }

    private bool EvaluateGreaterThan(object left, object right)
    {
        var leftNum = ConvertToDouble(left);
        var rightNum = ConvertToDouble(right);
        return leftNum > rightNum;
    }

    private bool EvaluateGreaterThanOrEqual(object left, object right)
    {
        var leftNum = ConvertToDouble(left);
        var rightNum = ConvertToDouble(right);
        return leftNum >= rightNum;
    }

    private bool TryConvertToDouble(object value, out double result)
    {
        if (value is double d)
        {
            result = d;
            return true;
        }

        if (value is int i)
        {
            result = i;
            return true;
        }

        if (value is string s && double.TryParse(s, out result))
        {
            return true;
        }

        result = 0;
        return false;
    }

    private double ConvertToDouble(object value)
    {
        if (!TryConvertToDouble(value, out var result))
        {
            throw new InvalidOperationException($"Cannot convert '{value}' to number");
        }
        return result;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var opSymbol = _operator switch
        {
            BinaryOperator.Add => "+",
            BinaryOperator.Subtract => "-",
            BinaryOperator.Multiply => "*",
            BinaryOperator.Divide => "/",
            BinaryOperator.Equal => "==",
            BinaryOperator.NotEqual => "!=",
            BinaryOperator.LessThan => "<",
            BinaryOperator.LessThanOrEqual => "<=",
            BinaryOperator.GreaterThan => ">",
            BinaryOperator.GreaterThanOrEqual => ">=",
            _ => "?"
        };

        return $"({_left} {opSymbol} {_right})";
    }
}
