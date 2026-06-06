using System.Globalization;

namespace PromptResponse.Core.Expressions;

/// <summary>The dynamic type of a <see cref="CelValue"/>.</summary>
public enum CelKind
{
    /// <summary>An unset / missing value (e.g. a referenced field that doesn't exist).</summary>
    Null,
    /// <summary>A string (the native type of every prompt response).</summary>
    String,
    /// <summary>A number (from a literal or <c>int()</c>/<c>double()</c>).</summary>
    Number,
    /// <summary>A boolean.</summary>
    Bool,
    /// <summary>A list of values (from a list literal or <c>exprSuggestValues</c>).</summary>
    List,
    /// <summary>A point in time, produced by <c>timestamp(...)</c>.</summary>
    Timestamp,
}

/// <summary>
/// A value in the expression language — the spec's CEL subset. Responses are
/// strings; numbers come from <c>int()</c>/<c>double()</c>, timestamps from
/// <c>timestamp()</c>. Immutable; no behavior beyond type-safe access.
/// </summary>
public readonly struct CelValue
{
    private readonly object? _value;

    private CelValue(CelKind kind, object? value)
    {
        Kind = kind;
        _value = value;
    }

    /// <summary>The dynamic kind of this value.</summary>
    public CelKind Kind { get; }

    /// <summary>The null/unset value.</summary>
    public static CelValue Null { get; } = new(CelKind.Null, null);

    /// <summary>Wraps a string value (null becomes empty).</summary>
    public static CelValue Of(string value) => new(CelKind.String, value ?? string.Empty);

    /// <summary>Wraps a numeric value.</summary>
    public static CelValue Of(double value) => new(CelKind.Number, value);

    /// <summary>Wraps a boolean value.</summary>
    public static CelValue Of(bool value) => new(CelKind.Bool, value);

    /// <summary>Wraps a timestamp value.</summary>
    public static CelValue Of(DateTimeOffset value) => new(CelKind.Timestamp, value);

    /// <summary>Wraps a list of values.</summary>
    public static CelValue List(IReadOnlyList<CelValue> items) => new(CelKind.List, items);

    /// <summary>True when this is the null/unset value.</summary>
    public bool IsNull => Kind == CelKind.Null;

    /// <summary>Gets the underlying string (only valid when <see cref="Kind"/> is String).</summary>
    public string AsString() => (string)_value!;

    /// <summary>Gets the underlying number (only valid when <see cref="Kind"/> is Number).</summary>
    public double AsNumber() => (double)_value!;

    /// <summary>Gets the underlying boolean (only valid when <see cref="Kind"/> is Bool).</summary>
    public bool AsBool() => (bool)_value!;

    /// <summary>Gets the underlying timestamp (only valid when <see cref="Kind"/> is Timestamp).</summary>
    public DateTimeOffset AsTimestamp() => (DateTimeOffset)_value!;

    /// <summary>Gets the underlying list (only valid when <see cref="Kind"/> is List).</summary>
    public IReadOnlyList<CelValue> AsList() => (IReadOnlyList<CelValue>)_value!;

    /// <summary>
    /// CEL "truthiness" for use where a boolean is required (e.g. <c>exprHidden</c>):
    /// a real bool; otherwise a non-empty string, a non-zero number, or a non-empty list.
    /// </summary>
    public bool IsTruthy => Kind switch
    {
        CelKind.Bool => AsBool(),
        CelKind.String => AsString().Length > 0,
        CelKind.Number => AsNumber() != 0,
        CelKind.List => AsList().Count > 0,
        CelKind.Timestamp => true,
        _ => false,
    };

    /// <summary>A stable, culture-invariant string rendering (used by <c>string(...)</c> and concatenation).</summary>
    public string ToDisplayString() => Kind switch
    {
        CelKind.Null => string.Empty,
        CelKind.String => AsString(),
        CelKind.Bool => AsBool() ? "true" : "false",
        CelKind.Number => AsNumber().ToString("0.###############", CultureInfo.InvariantCulture),
        CelKind.Timestamp => AsTimestamp().ToString("o", CultureInfo.InvariantCulture),
        CelKind.List => "[" + string.Join(", ", AsList().Select(v => v.ToDisplayString())) + "]",
        _ => string.Empty,
    };

    /// <summary>Debug rendering as <c>Kind:value</c>.</summary>
    public override string ToString() => $"{Kind}:{ToDisplayString()}";
}
