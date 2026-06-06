namespace PromptResponse.Core.Expressions;

/// <summary>
/// A parsed, reusable expression. Compile once, evaluate many times against
/// different <see cref="IExpressionContext"/> snapshots (cheap and allocation-light).
/// </summary>
public sealed class CelExpression
{
    private readonly Node _root;

    internal CelExpression(Node root) => _root = root;

    /// <summary>Evaluates to a typed value. Throws <see cref="ExpressionException"/> on a type/eval error.</summary>
    public CelValue Evaluate(IExpressionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new Evaluator(context).Eval(_root);
    }

    /// <summary>
    /// Evaluates to a boolean (CEL truthiness), returning <paramref name="fallback"/>
    /// if the expression errors — so a broken <c>exprHidden</c>/<c>exprExpected</c>
    /// never blocks the form.
    /// </summary>
    public bool EvaluateBool(IExpressionContext context, bool fallback = false)
    {
        try
        {
            return Evaluate(context).IsTruthy;
        }
        catch (ExpressionException)
        {
            return fallback;
        }
    }

    /// <summary>Evaluates to a display string, returning <paramref name="fallback"/> on error.</summary>
    public string EvaluateString(IExpressionContext context, string fallback = "")
    {
        try
        {
            return Evaluate(context).ToDisplayString();
        }
        catch (ExpressionException)
        {
            return fallback;
        }
    }
}

/// <summary>
/// Compiles and evaluates expressions in the spec's CEL subset (Appendix B/C):
/// prompt ids are variables holding response strings, with <c>_this</c>/<c>_today</c>
/// built-ins and <c>ctx.*</c> context. Pure data — no code execution, bounded
/// nesting/length, regex timeouts.
/// </summary>
public static class ExpressionEvaluator
{
    /// <summary>Maximum source length accepted, as a denial-of-service guard.</summary>
    public const int MaxExpressionLength = 4000;

    /// <summary>Parses an expression into a reusable <see cref="CelExpression"/>.</summary>
    /// <exception cref="ExpressionException">On a syntax error or limit breach.</exception>
    public static CelExpression Compile(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        if (expression.Length > MaxExpressionLength)
        {
            throw new ExpressionException($"Expression exceeds the {MaxExpressionLength}-character limit.");
        }

        var tokens = new Lexer(expression).Tokenize();
        var root = new Parser(tokens).ParseProgram();
        return new CelExpression(root);
    }

    /// <summary>Compiles and evaluates in one call.</summary>
    public static CelValue Evaluate(string expression, IExpressionContext context) =>
        Compile(expression).Evaluate(context);

    /// <summary>
    /// Convenience: evaluate to bool with graceful fallback (used by visibility /
    /// required hints). Compile errors also fall back.
    /// </summary>
    public static bool EvaluateBool(string expression, IExpressionContext context, bool fallback = false)
    {
        try
        {
            return Compile(expression).EvaluateBool(context, fallback);
        }
        catch (ExpressionException)
        {
            return fallback;
        }
    }
}
