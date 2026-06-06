namespace PromptResponse.Core.Expressions;

/// <summary>
/// Thrown when an expression cannot be parsed or evaluated (syntax error, type
/// error, unknown function, or a safety limit being exceeded).
/// </summary>
/// <remarks>
/// Callers that drive UI hints (e.g. <c>exprHidden</c>) should catch this and
/// fall back to a safe default rather than surfacing a hard error — a broken
/// hint must never block the user from filling the form.
/// </remarks>
public sealed class ExpressionException : Exception
{
    /// <summary>Creates the exception with a message.</summary>
    public ExpressionException(string message) : base(message)
    {
    }

    /// <summary>Creates the exception with a message and an inner cause.</summary>
    public ExpressionException(string message, Exception inner) : base(message, inner)
    {
    }
}
