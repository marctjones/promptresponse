namespace PromptResponse.Core.Expressions;

/// <summary>
/// Resolves the variables an expression references. Per the spec, each prompt id
/// is a variable holding its response string; <c>_this</c> is the current
/// prompt's value, <c>_today</c> is today's date, and <c>ctx.*</c> reaches the
/// optional filler-provided context.
/// </summary>
/// <remarks>
/// Resolution must be side-effect free. A missing variable returns
/// <see cref="CelValue.Null"/> so expressions degrade gracefully (the spec
/// requires expressions to tolerate missing context) rather than throwing.
/// </remarks>
public interface IExpressionContext
{
    /// <summary>
    /// Resolves a top-level name (a prompt id, a built-in like <c>_this</c>/<c>_today</c>,
    /// or the <c>ctx</c> root). Returns <see cref="CelValue.Null"/> when unknown.
    /// </summary>
    CelValue Resolve(string name);
}
