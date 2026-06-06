using PromptResponse.Core.Models;

namespace PromptResponse.Core.Expressions;

/// <summary>
/// Evaluates a document's <c>expr*</c> hints against the current responses:
/// conditional visibility, computed (read-only) values, conditional-expected,
/// and cross-field validation. Pure and side-effect-free except
/// <see cref="RecomputeComputedValues"/>, which writes derived values back.
/// </summary>
/// <remarks>
/// Per the vision these are advisory: a broken expression falls back to a safe
/// default (visible, not required, valid, unchanged) and never blocks input.
/// </remarks>
public static class FormExpressions
{
    /// <summary>All prompts in the document, in document order (sections recursed).</summary>
    public static IReadOnlyList<Prompt> GetAllPrompts(AprDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var list = new List<Prompt>();
        foreach (var section in document.Sections)
        {
            Collect(section, list);
        }
        return list;
    }

    private static void Collect(Section section, List<Prompt> into)
    {
        into.AddRange(section.Prompts);
        foreach (var child in section.Sections)
        {
            Collect(child, into);
        }
    }

    /// <summary>Snapshot of prompt id → current response, the variable scope for expressions.</summary>
    public static IReadOnlyDictionary<string, string> BuildFields(AprDocument document)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in GetAllPrompts(document))
        {
            if (!string.IsNullOrEmpty(p.Id))
            {
                fields[p.Id] = p.Response;
            }
        }
        return fields;
    }

    /// <summary>True when the prompt's <c>exprHidden</c> evaluates truthy (default: visible).</summary>
    public static bool IsHidden(Prompt prompt, IReadOnlyDictionary<string, string> fields, string? today = null, IReadOnlyDictionary<string, string>? ctx = null) =>
        EvalBool(prompt, prompt.Hints.ExprHidden, fields, today, ctx, fallback: false);

    /// <summary>True when the prompt's <c>exprExpected</c> evaluates truthy (default: not required).</summary>
    public static bool IsExpected(Prompt prompt, IReadOnlyDictionary<string, string> fields, string? today = null, IReadOnlyDictionary<string, string>? ctx = null) =>
        EvalBool(prompt, prompt.Hints.ExprExpected, fields, today, ctx, fallback: false);

    /// <summary>
    /// True when the prompt is read-only: it has a computed value (<c>exprValue</c>)
    /// or its <c>exprReadOnly</c> evaluates truthy.
    /// </summary>
    public static bool IsReadOnly(Prompt prompt, IReadOnlyDictionary<string, string> fields, string? today = null, IReadOnlyDictionary<string, string>? ctx = null) =>
        !string.IsNullOrWhiteSpace(prompt.Hints.ExprValue)
        || EvalBool(prompt, prompt.Hints.ExprReadOnly, fields, today, ctx, fallback: false);

    /// <summary>
    /// The cross-field validation message from <c>exprValidation</c>, or null when
    /// there's no rule or the field is valid (empty result).
    /// </summary>
    public static string? Validate(Prompt prompt, IReadOnlyDictionary<string, string> fields, string? today = null, IReadOnlyDictionary<string, string>? ctx = null)
    {
        if (string.IsNullOrWhiteSpace(prompt.Hints.ExprValidation))
        {
            return null;
        }
        var message = EvalString(prompt, prompt.Hints.ExprValidation!, fields, today, ctx, fallback: string.Empty);
        return string.IsNullOrEmpty(message) ? null : message;
    }

    /// <summary>
    /// The computed value from <c>exprValue</c>, or null when the prompt isn't
    /// computed. Errors fall back to the prompt's current response.
    /// </summary>
    public static string? ComputeValue(Prompt prompt, IReadOnlyDictionary<string, string> fields, string? today = null, IReadOnlyDictionary<string, string>? ctx = null)
    {
        if (string.IsNullOrWhiteSpace(prompt.Hints.ExprValue))
        {
            return null;
        }
        return EvalString(prompt, prompt.Hints.ExprValue!, fields, today, ctx, fallback: prompt.Response);
    }

    /// <summary>
    /// Recomputes every computed field (<c>exprValue</c>) and writes the result
    /// back to its response, iterating to a fixpoint so chained computations
    /// (a total of subtotals) settle. Bounded to defuse circular references.
    /// Returns true if any value changed.
    /// </summary>
    public static bool RecomputeComputedValues(AprDocument document, string? today = null, IReadOnlyDictionary<string, string>? ctx = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var computed = GetAllPrompts(document)
            .Where(p => !string.IsNullOrWhiteSpace(p.Hints.ExprValue))
            .ToList();
        if (computed.Count == 0)
        {
            return false;
        }

        var anyChanged = false;
        // At most computed.Count+1 passes: each pass can settle at least one more
        // field in a non-circular dependency graph; the extra pass confirms stability.
        for (var pass = 0; pass <= computed.Count; pass++)
        {
            var fields = BuildFields(document);
            var changedThisPass = false;
            foreach (var p in computed)
            {
                var value = EvalString(p, p.Hints.ExprValue!, fields, today, ctx, fallback: p.Response);
                if (value != p.Response)
                {
                    p.Response = value;
                    changedThisPass = true;
                    anyChanged = true;
                }
            }
            if (!changedThisPass)
            {
                break;
            }
        }
        return anyChanged;
    }

    private static IExpressionContext ContextFor(Prompt prompt, IReadOnlyDictionary<string, string> fields, string? today, IReadOnlyDictionary<string, string>? ctx) =>
        new DictionaryExpressionContext(fields, thisValue: prompt.Response, today: today, ctx: ctx);

    private static bool EvalBool(Prompt prompt, string? expr, IReadOnlyDictionary<string, string> fields, string? today, IReadOnlyDictionary<string, string>? ctx, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(expr))
        {
            return fallback;
        }
        return ExpressionEvaluator.EvaluateBool(expr!, ContextFor(prompt, fields, today, ctx), fallback);
    }

    private static string EvalString(Prompt prompt, string expr, IReadOnlyDictionary<string, string> fields, string? today, IReadOnlyDictionary<string, string>? ctx, string fallback)
    {
        try
        {
            return ExpressionEvaluator.Compile(expr).EvaluateString(ContextFor(prompt, fields, today, ctx), fallback);
        }
        catch (ExpressionException)
        {
            return fallback;
        }
    }
}
