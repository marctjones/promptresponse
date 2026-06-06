namespace PromptResponse.Core.Expressions;

/// <summary>
/// A simple <see cref="IExpressionContext"/> backed by dictionaries: prompt ids
/// → response strings, the <c>_this</c>/<c>_today</c> built-ins, and an optional
/// <c>ctx.*</c> map keyed by dotted path (e.g. <c>"user.role"</c> for
/// <c>ctx.user.role</c>). Unknown names resolve to <see cref="CelValue.Null"/>.
/// </summary>
public sealed class DictionaryExpressionContext : IExpressionContext
{
    private readonly IReadOnlyDictionary<string, string> _fields;
    private readonly IReadOnlyDictionary<string, string>? _ctx;
    private readonly string? _thisValue;
    private readonly string? _today;

    /// <summary>
    /// Creates the context.
    /// </summary>
    /// <param name="fields">Prompt id → response string.</param>
    /// <param name="thisValue">Value of <c>_this</c> (the current prompt's response).</param>
    /// <param name="today">Value of <c>_today</c> (an ISO date string for <c>timestamp(_today)</c>).</param>
    /// <param name="ctx">Context values keyed by dotted path under <c>ctx</c> (e.g. "user.role").</param>
    public DictionaryExpressionContext(
        IReadOnlyDictionary<string, string> fields,
        string? thisValue = null,
        string? today = null,
        IReadOnlyDictionary<string, string>? ctx = null)
    {
        _fields = fields ?? throw new ArgumentNullException(nameof(fields));
        _thisValue = thisValue;
        _today = today;
        _ctx = ctx;
    }

    /// <inheritdoc />
    public CelValue Resolve(string name)
    {
        switch (name)
        {
            case "_this":
                return _thisValue is null ? CelValue.Null : CelValue.Of(_thisValue);
            case "_today":
                return _today is null ? CelValue.Null : CelValue.Of(_today);
            case "ctx":
                return CelValue.Null; // the bare namespace is not itself a value
        }

        if (name.StartsWith("ctx.", StringComparison.Ordinal))
        {
            var key = name["ctx.".Length..];
            return _ctx != null && _ctx.TryGetValue(key, out var cv) ? CelValue.Of(cv) : CelValue.Null;
        }

        return _fields.TryGetValue(name, out var fv) ? CelValue.Of(fv) : CelValue.Null;
    }
}
