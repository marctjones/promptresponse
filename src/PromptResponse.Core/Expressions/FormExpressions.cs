using PromptResponse.Core.Models;

namespace PromptResponse.Core.Expressions;

/// <summary>
/// Evaluates the advisory <c>expr*</c> hints over a document.
/// </summary>
/// <remarks>
/// <para>
/// Expressions are CEL (specification section 8), with each prompt's
/// <c>expectedDataType</c> supplying the type environment. They are hints like any
/// other: they never reject a response, never block saving, and never make a document
/// invalid. Anything that cannot be evaluated degrades to the stored response, so a
/// broken expression costs the author a correction and costs the filler nothing.
/// </para>
/// </remarks>
public static class FormExpressions
{
    /// <summary>Marks a response as produced by <c>exprValue</c> rather than authored.</summary>
    public const string ComputedSource = "computed";

    /// <summary>Every prompt in the document, in document order.</summary>
    public static IReadOnlyList<Prompt> GetAllPrompts(AprDocument document)
        => PromptTreeTraversal.GetAll(document);

    /// <summary>Builds the evaluation environment for a document.</summary>
    public static FormExpressionContext BuildContext(
        AprDocument document,
        string? today = null,
        IReadOnlyDictionary<string, string>? ctx = null) =>
        FormExpressionContext.Create(document, today, ctx);

    /// <summary>Whether this prompt should be hidden.</summary>
    public static bool IsHidden(Prompt prompt, FormExpressionContext context) =>
        EvaluateCondition(prompt, prompt.Hints?.ExprHidden, context);

    /// <summary>Whether this prompt is marked as expected. Advisory; never blocks submission.</summary>
    public static bool IsExpected(Prompt prompt, FormExpressionContext context) =>
        EvaluateCondition(prompt, prompt.Hints?.ExprExpected, context);

    /// <summary>Whether this prompt should be presented read-only.</summary>
    /// <remarks>
    /// Read-only is a <em>presentation</em> hint, never a lock. Any string is a valid
    /// response, so a renderer that refuses to accept typing has stopped implementing
    /// the format — a computed total a person needs to correct must be correctable.
    ///
    /// Being computed does not make a field read-only. That is what
    /// <see cref="PromptHints.ExprReadOnly"/> is for, and even then a renderer should
    /// offer a way in rather than a wall.
    /// </remarks>
    public static bool IsReadOnly(Prompt prompt, FormExpressionContext context) =>
        EvaluateCondition(prompt, prompt.Hints?.ExprReadOnly, context);

    /// <summary>Whether this prompt's value is derived from an expression.</summary>
    /// <remarks>
    /// Separate from <see cref="IsReadOnly"/> on purpose: a renderer may want to show a
    /// computed field differently — a badge, a subdued style, a "recalculate" affordance —
    /// without preventing anyone from typing into it.
    /// </remarks>
    public static bool IsComputed(Prompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return !string.IsNullOrWhiteSpace(prompt.Hints?.ExprValue);
    }

    private static bool EvaluateCondition(Prompt prompt, string? expression, FormExpressionContext context)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }
        var result = context.Evaluate(expression, prompt);
        // CEL requires bool in a condition, so an error or any other type is simply not
        // true: an advisory hint that cannot be evaluated does not apply.
        return result is not null && CelBinding.IsTrue(result);
    }

    /// <summary>
    /// The cross-field validation message for this prompt, or null when it is fine.
    /// </summary>
    /// <remarks>Always advisory. A message never makes a document invalid.</remarks>
    public static string? Validate(Prompt prompt, FormExpressionContext context)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(context);
        var expression = prompt.Hints?.ExprValidation;
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }
        var result = context.Evaluate(expression, prompt);
        if (result is null)
        {
            return null;
        }
        var message = CelBinding.ToStoredString(result);
        return string.IsNullOrEmpty(message) ? null : message;
    }

    /// <summary>
    /// The computed value for this prompt, or null when there is none or it cannot be
    /// computed.
    /// </summary>
    public static string? ComputeValue(Prompt prompt, FormExpressionContext context)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(context);
        var expression = prompt.Hints?.ExprValue;
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }
        var result = context.Evaluate(expression, prompt);
        return result is null ? null : CelBinding.ToStoredString(result);
    }

    /// <summary>
    /// Recomputes every computed field in the document. Returns true when anything
    /// changed.
    /// </summary>
    /// <remarks>
    /// A computed value is a convenience, not an authority. An expression that cannot be
    /// evaluated leaves the stored response alone rather than clearing it, so a filler
    /// never loses an answer to a broken formula.
    /// </remarks>
    public static bool RecomputeComputedValues(
        AprDocument document,
        string? today = null,
        IReadOnlyDictionary<string, string>? ctx = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        return ComputedValueRecalculator.Recompute(document, today, ctx);
    }
}
