using System.Globalization;
using Celly.Types;
using PromptResponse.Core.Models;

namespace PromptResponse.Core.Expressions;

/// <summary>
/// Translates between APR's string-only data and CEL's static type system.
/// </summary>
/// <remarks>
/// <para>
/// APR stores every value as a string; CEL is statically typed. The impedance is
/// resolved here, in the binding, rather than by weakening either side: CEL stays
/// exactly CEL, and the format keeps its rule that any string is a valid response.
/// </para>
/// <para>
/// <c>expectedDataType</c> supplies the type environment. That is the hint doing real
/// work without constraining anything: a response that will not bind to its declared
/// type is simply absent from the evaluation, so the expression errors and degrades to
/// the stored response (specification section 8.3). The answer is still stored
/// verbatim, still displayed, and still valid — it just does not participate in a
/// calculation.
/// </para>
/// </remarks>
internal static class CelBinding
{
    /// <summary>The CEL type a prompt's declared hint maps to (specification section 8.5).</summary>
    public static CelType TypeFor(string? expectedDataType) =>
        (expectedDataType ?? string.Empty).ToLowerInvariant() switch
        {
            "number" or "currency" => CelType.Double,
            "boolean" => CelType.Bool,
            "date" or "time" or "datetime" => CelType.Timestamp,
            "multichoice" => CelType.ListDyn,
            _ => CelType.String,
        };

    /// <summary>Generous read set for booleans (specification section 4.9).</summary>
    private static bool? AsBoolean(string value) => value.Trim().ToLowerInvariant() switch
    {
        "true" or "yes" or "y" or "1" or "on" or "x" or "checked" => true,
        "false" or "no" or "n" or "0" or "off" or "unchecked" => false,
        _ => null,
    };

    /// <summary>
    /// Converts a stored response to the CLR value CEL expects for its declared type,
    /// or null when it will not bind.
    /// </summary>
    /// <remarks>
    /// Returning null is deliberate and is never a default. Binding an unparseable
    /// number as zero would make a blank or free-text field silently total as 0 — a
    /// wrong answer rather than no answer. Omitting it produces a CEL error, which
    /// degrades to the stored response.
    /// </remarks>
    public static object? Bind(string? response, CelType declared)
    {
        var value = response ?? string.Empty;

        if (declared == CelType.String)
        {
            return value;   // any string binds; this is the whole point of the format
        }
        if (declared == CelType.ListDyn)
        {
            // Canonical multichoice is newline-separated; the legacy comma form is
            // still read. An empty response is no selection, which is a real answer.
            if (value.Length == 0) return Array.Empty<object?>();
            var parts = value.Contains('\n') ? value.Split('\n') : value.Split(',');
            return parts.Select(p => (object?)p.Trim()).Where(p => ((string)p!).Length > 0).ToArray();
        }
        if (value.Trim().Length == 0)
        {
            return null;    // empty is unbindable for every typed field
        }
        if (declared == CelType.Double)
        {
            return double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                ? d : null;
        }
        if (declared == CelType.Bool)
        {
            return AsBoolean(value);
        }
        if (declared == CelType.Timestamp)
        {
            // CEL timestamps are seconds-plus-nanos since the epoch, not a CLR date type.
            if (!DateTimeOffset.TryParse(value.Trim(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var ts))
            {
                return null;
            }
            var seconds = ts.ToUnixTimeSeconds();
            var nanos = (int)((ts.ToUnixTimeMilliseconds() - seconds * 1000L) * 1_000_000L);
            return Celly.Values.TimestampValue.Of(seconds, nanos);
        }
        return value;
    }

    /// <summary>
    /// Renders a CEL result back to a stored string using the canonical write forms of
    /// specification section 4.9, which serve both directions. Returns null for an
    /// error or unknown result, which the caller degrades to the stored response.
    /// </summary>
    public static string? ToStoredString(Celly.Values.CelValue value)
    {
        if (value.IsError || value.IsUnknown)
        {
            return null;
        }

        return value.ToNative() switch
        {
            null => string.Empty,
            bool b => b ? "true" : "false",
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            float f => ((double)f).ToString("R", CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            ulong u => u.ToString(CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            Celly.Values.CelTimestampData ts =>
                DateTimeOffset.FromUnixTimeSeconds(ts.Seconds)
                    .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            DateTime dt => dt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            string s => s,
            System.Collections.IEnumerable seq => string.Join("\n", seq.Cast<object?>().Select(x => x?.ToString() ?? string.Empty)),
            var other => other.ToString() ?? string.Empty,
        };
    }

    /// <summary>Whether a CEL result should be treated as true.</summary>
    /// <remarks>
    /// CEL requires bool in conditions, so anything else - including an error from an
    /// unbindable field - is not true. An advisory hint that cannot be evaluated simply
    /// does not apply.
    /// </remarks>
    public static bool IsTrue(Celly.Values.CelValue value) =>
        !value.IsError && !value.IsUnknown && value.ToNative() is bool b && b;
}
