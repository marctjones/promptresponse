using System.Text;
using System.Text.RegularExpressions;

namespace PromptResponse.Core.Expressions;

/// <summary>
/// Parses expression strings into expression trees.
/// </summary>
/// <remarks>
/// Supports syntax:
/// - Field references: {fieldId}
/// - Numbers: 123, 123.45
/// - Strings: "text"
/// - Arithmetic: +, -, *, /, ()
/// - Comparison: ==, !=, &lt;, &gt;, &lt;=, &gt;=
/// - Functions: SUM({field1}, {field2}), IF(condition, trueVal, falseVal)
/// </remarks>
public class ExpressionParser
{
    private string _expression = "";
    private int _position;

    /// <summary>
    /// Parses an expression string.
    /// </summary>
    public IExpression Parse(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("Expression cannot be empty", nameof(expression));

        _expression = expression;
        _position = 0;

        try
        {
            var result = ParseExpression();

            // Ensure we consumed the entire expression
            SkipWhitespace();
            if (_position < _expression.Length)
            {
                throw new ExpressionParseException(
                    $"Unexpected character '{_expression[_position]}' at position {_position}");
            }

            return result;
        }
        catch (Exception ex) when (ex is not ExpressionParseException)
        {
            throw new ExpressionParseException($"Failed to parse expression: {ex.Message}", ex);
        }
    }

    private IExpression ParseExpression()
    {
        return ParseAdditive();
    }

    private IExpression ParseAdditive()
    {
        var left = ParseMultiplicative();

        while (true)
        {
            SkipWhitespace();
            if (_position >= _expression.Length)
                break;

            char op = _expression[_position];
            if (op == '+' || op == '-')
            {
                _position++;
                var right = ParseMultiplicative();
                left = new BinaryExpression(left, op == '+' ? BinaryOperator.Add : BinaryOperator.Subtract, right);
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private IExpression ParseMultiplicative()
    {
        var left = ParsePrimary();

        while (true)
        {
            SkipWhitespace();
            if (_position >= _expression.Length)
                break;

            char op = _expression[_position];
            if (op == '*' || op == '/')
            {
                _position++;
                var right = ParsePrimary();
                left = new BinaryExpression(left, op == '*' ? BinaryOperator.Multiply : BinaryOperator.Divide, right);
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private IExpression ParsePrimary()
    {
        SkipWhitespace();

        if (_position >= _expression.Length)
            throw new ExpressionParseException("Unexpected end of expression");

        char current = _expression[_position];

        // Parentheses
        if (current == '(')
        {
            _position++;
            var expr = ParseExpression();
            SkipWhitespace();
            if (_position >= _expression.Length || _expression[_position] != ')')
                throw new ExpressionParseException("Missing closing parenthesis");
            _position++;
            return expr;
        }

        // Field reference: {fieldId}
        if (current == '{')
        {
            return ParseFieldReference();
        }

        // String literal: "text"
        if (current == '"')
        {
            return ParseStringLiteral();
        }

        // Number or function name
        if (char.IsDigit(current) || current == '-' || current == '.')
        {
            return ParseNumber();
        }

        // Function call
        if (char.IsLetter(current))
        {
            return ParseFunctionCall();
        }

        throw new ExpressionParseException($"Unexpected character '{current}' at position {_position}");
    }

    private IExpression ParseFieldReference()
    {
        _position++; // Skip '{'

        var sb = new StringBuilder();
        while (_position < _expression.Length && _expression[_position] != '}')
        {
            sb.Append(_expression[_position]);
            _position++;
        }

        if (_position >= _expression.Length)
            throw new ExpressionParseException("Missing closing brace for field reference");

        _position++; // Skip '}'

        return new FieldReferenceExpression(sb.ToString());
    }

    private IExpression ParseStringLiteral()
    {
        _position++; // Skip opening quote

        var sb = new StringBuilder();
        while (_position < _expression.Length && _expression[_position] != '"')
        {
            if (_expression[_position] == '\\' && _position + 1 < _expression.Length)
            {
                _position++;
                sb.Append(_expression[_position]);
            }
            else
            {
                sb.Append(_expression[_position]);
            }
            _position++;
        }

        if (_position >= _expression.Length)
            throw new ExpressionParseException("Missing closing quote for string literal");

        _position++; // Skip closing quote

        return new LiteralExpression(sb.ToString());
    }

    private IExpression ParseNumber()
    {
        var sb = new StringBuilder();

        // Handle negative sign
        if (_expression[_position] == '-')
        {
            sb.Append(_expression[_position]);
            _position++;
        }

        while (_position < _expression.Length &&
               (char.IsDigit(_expression[_position]) || _expression[_position] == '.'))
        {
            sb.Append(_expression[_position]);
            _position++;
        }

        if (!double.TryParse(sb.ToString(), out var value))
            throw new ExpressionParseException($"Invalid number format: {sb}");

        return new LiteralExpression(value);
    }

    private IExpression ParseFunctionCall()
    {
        var sb = new StringBuilder();

        // Read function name
        while (_position < _expression.Length && char.IsLetterOrDigit(_expression[_position]))
        {
            sb.Append(_expression[_position]);
            _position++;
        }

        var functionName = sb.ToString();

        SkipWhitespace();

        if (_position >= _expression.Length || _expression[_position] != '(')
            throw new ExpressionParseException($"Expected '(' after function name '{functionName}'");

        _position++; // Skip '('

        // Parse arguments
        var arguments = new List<IExpression>();

        SkipWhitespace();
        if (_position < _expression.Length && _expression[_position] != ')')
        {
            while (true)
            {
                arguments.Add(ParseExpression());

                SkipWhitespace();
                if (_position >= _expression.Length)
                    throw new ExpressionParseException("Missing closing parenthesis in function call");

                if (_expression[_position] == ')')
                    break;

                if (_expression[_position] != ',')
                    throw new ExpressionParseException("Expected ',' or ')' in function call");

                _position++; // Skip ','
            }
        }

        _position++; // Skip ')'

        return new FunctionCallExpression(functionName, arguments);
    }

    private void SkipWhitespace()
    {
        while (_position < _expression.Length && char.IsWhiteSpace(_expression[_position]))
        {
            _position++;
        }
    }
}

/// <summary>
/// Exception thrown when expression parsing fails.
/// </summary>
public class ExpressionParseException : Exception
{
    public ExpressionParseException(string message) : base(message) { }
    public ExpressionParseException(string message, Exception innerException) : base(message, innerException) { }
}
