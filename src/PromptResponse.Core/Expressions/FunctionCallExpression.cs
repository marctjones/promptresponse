namespace PromptResponse.Core.Expressions;

/// <summary>
/// Represents a function call expression.
/// </summary>
public class FunctionCallExpression : IExpression
{
    private readonly string _functionName;
    private readonly List<IExpression> _arguments;

    /// <summary>
    /// Gets the name of the function being called.
    /// </summary>
    public string FunctionName => _functionName;

    /// <summary>
    /// Gets the list of argument expressions.
    /// </summary>
    public IReadOnlyList<IExpression> Arguments => _arguments;

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionCallExpression"/> class.
    /// </summary>
    /// <param name="functionName">The name of the function to call.</param>
    /// <param name="arguments">The list of argument expressions.</param>
    public FunctionCallExpression(string functionName, List<IExpression> arguments)
    {
        if (string.IsNullOrWhiteSpace(functionName))
            throw new ArgumentException("Function name cannot be empty", nameof(functionName));

        _functionName = functionName;
        _arguments = arguments ?? new List<IExpression>();
    }

    /// <inheritdoc/>
    public object Evaluate(IEvaluationContext context)
    {
        return _functionName.ToUpperInvariant() switch
        {
            "SUM" => EvaluateSum(context),
            "AVG" or "AVERAGE" => EvaluateAverage(context),
            "COUNT" => EvaluateCount(context),
            "MIN" => EvaluateMin(context),
            "MAX" => EvaluateMax(context),
            "IF" => EvaluateIf(context),
            "ROUND" => EvaluateRound(context),
            "ABS" => EvaluateAbs(context),
            _ => throw new InvalidOperationException($"Unknown function: {_functionName}")
        };
    }

    private object EvaluateSum(IEvaluationContext context)
    {
        double sum = 0;
        foreach (var arg in _arguments)
        {
            var value = arg.Evaluate(context);
            sum += ConvertToDouble(value);
        }
        return sum;
    }

    private object EvaluateAverage(IEvaluationContext context)
    {
        if (_arguments.Count == 0)
            return 0.0;

        double sum = 0;
        int count = 0;

        foreach (var arg in _arguments)
        {
            var value = arg.Evaluate(context);
            if (TryConvertToDouble(value, out var numValue))
            {
                sum += numValue;
                count++;
            }
        }

        return count > 0 ? sum / count : 0.0;
    }

    private object EvaluateCount(IEvaluationContext context)
    {
        int count = 0;
        foreach (var arg in _arguments)
        {
            var value = arg.Evaluate(context);
            var str = value?.ToString() ?? "";
            if (!string.IsNullOrWhiteSpace(str))
            {
                count++;
            }
        }
        return count;
    }

    private object EvaluateMin(IEvaluationContext context)
    {
        if (_arguments.Count == 0)
            throw new InvalidOperationException("MIN requires at least one argument");

        double min = double.MaxValue;
        foreach (var arg in _arguments)
        {
            var value = arg.Evaluate(context);
            var numValue = ConvertToDouble(value);
            if (numValue < min)
                min = numValue;
        }
        return min;
    }

    private object EvaluateMax(IEvaluationContext context)
    {
        if (_arguments.Count == 0)
            throw new InvalidOperationException("MAX requires at least one argument");

        double max = double.MinValue;
        foreach (var arg in _arguments)
        {
            var value = arg.Evaluate(context);
            var numValue = ConvertToDouble(value);
            if (numValue > max)
                max = numValue;
        }
        return max;
    }

    private object EvaluateIf(IEvaluationContext context)
    {
        if (_arguments.Count != 3)
            throw new InvalidOperationException("IF requires exactly 3 arguments: condition, trueValue, falseValue");

        var condition = _arguments[0].Evaluate(context);
        var isTrue = ConvertToBoolean(condition);

        return isTrue
            ? _arguments[1].Evaluate(context)
            : _arguments[2].Evaluate(context);
    }

    private object EvaluateRound(IEvaluationContext context)
    {
        if (_arguments.Count == 0 || _arguments.Count > 2)
            throw new InvalidOperationException("ROUND requires 1 or 2 arguments: value, [decimals]");

        var value = ConvertToDouble(_arguments[0].Evaluate(context));
        var decimals = _arguments.Count > 1
            ? (int)ConvertToDouble(_arguments[1].Evaluate(context))
            : 0;

        return Math.Round(value, decimals);
    }

    private object EvaluateAbs(IEvaluationContext context)
    {
        if (_arguments.Count != 1)
            throw new InvalidOperationException("ABS requires exactly 1 argument");

        var value = ConvertToDouble(_arguments[0].Evaluate(context));
        return Math.Abs(value);
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

    private bool ConvertToBoolean(object value)
    {
        if (value is bool b)
            return b;

        if (value is string s)
        {
            if (bool.TryParse(s, out var boolValue))
                return boolValue;

            // Non-empty strings are true
            return !string.IsNullOrWhiteSpace(s) && s != "0" && s.ToLowerInvariant() != "false";
        }

        if (TryConvertToDouble(value, out var numValue))
        {
            return Math.Abs(numValue) > double.Epsilon;
        }

        return value != null;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var args = string.Join(", ", _arguments.Select(a => a.ToString()));
        return $"{_functionName}({args})";
    }
}
