using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Core.Beta6;

/// <summary>APR beta.6 semantic digest and integrity-manifest helpers.</summary>
public static class AprSemanticDigest
{
    /// <summary>The canonicalization identifier carried by beta.6 attestations.</summary>
    public const string Canonicalization = "jcs-sha256";

    /// <summary>Returns the canonical UTF-8 JSON bytes used for a semantic digest.</summary>
    public static byte[] Canonicalize(JsonElement value)
    {
        var builder = new StringBuilder();
        WriteCanonical(builder, value);
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    /// <summary>Returns a lowercase, prefixed SHA-256 semantic digest.</summary>
    public static string Digest(JsonElement value) => DigestBytes(Canonicalize(value));

    /// <summary>Creates the complete leaf manifest for a form semantic model.</summary>
    public static AprIntegrityManifest CreateManifest(JsonElement form)
    {
        var entries = new List<AprManifestEntry>();
        AddEntries(form, "", entries);
        return new AprIntegrityManifest(Digest(form), entries);
    }

    private static string DigestBytes(byte[] bytes) => "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void AddEntries(JsonElement value, string path, List<AprManifestEntry> entries)
    {
        entries.Add(new AprManifestEntry(path, Digest(value)));
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                    AddEntries(property.Value, path + "/" + EscapePointer(property.Name), entries);
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in value.EnumerateArray()) AddEntries(item, path + "/" + index++, entries);
                break;
        }
    }

    private static string EscapePointer(string value) => value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    /// <summary>Appends the RFC 8785 canonical form of <paramref name="value"/>.</summary>
    /// <remarks>
    /// Written directly rather than through <see cref="Utf8JsonWriter"/>. No built-in
    /// encoder produces JCS: the default escapes for HTML safety, and even
    /// <c>UnsafeRelaxedJsonEscaping</c> escapes a character outside the Basic
    /// Multilingual Plane as a surrogate pair, so an emoji in a title produced
    /// <c>\uD83D\uDE00</c> where the canonical bytes are the character's own four UTF-8
    /// bytes — a digest no other implementation could reproduce.
    /// </remarks>
    private static void WriteCanonical(StringBuilder builder, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                var firstMember = true;
                // Member order is by UTF-16 code unit, which is what Ordinal compares.
                foreach (var property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    if (!firstMember) builder.Append(',');
                    firstMember = false;
                    builder.Append(CanonicalString(property.Name)).Append(':');
                    WriteCanonical(builder, property.Value);
                }
                builder.Append('}');
                break;
            case JsonValueKind.Array:
                builder.Append('[');
                var firstItem = true;
                foreach (var item in value.EnumerateArray())
                {
                    if (!firstItem) builder.Append(',');
                    firstItem = false;
                    WriteCanonical(builder, item);
                }
                builder.Append(']');
                break;
            case JsonValueKind.String: builder.Append(CanonicalString(value.GetString()!)); break;
            case JsonValueKind.True: builder.Append("true"); break;
            case JsonValueKind.False: builder.Append("false"); break;
            case JsonValueKind.Null: builder.Append("null"); break;
            case JsonValueKind.Number:
                if (!value.TryGetDouble(out var number))
                    throw new SerializationException("APR semantic digests require finite JSON numbers.");
                builder.Append(CanonicalNumber(number));
                break;
            default: throw new SerializationException("Unsupported JSON value in APR semantic digest.");
        }
    }

    /// <summary>RFC 8785 section 3.2.2.2 string serialization, as a quoted literal.</summary>
    /// <remarks>
    /// JCS escapes exactly what JSON mandates — the quote, the backslash, and the C0
    /// controls, using the two-character forms where they exist and <c>\u00xx</c>
    /// otherwise — and leaves every other character as itself.
    ///
    /// This is written out rather than delegated because no built-in encoder does it.
    /// The default one escapes for HTML safety, and even
    /// <c>UnsafeRelaxedJsonEscaping</c> escapes a character outside the Basic
    /// Multilingual Plane as a surrogate pair: an emoji in a title came out
    /// <c>\uD83D\uDE00</c> where the canonical bytes are the four UTF-8 bytes of the
    /// character itself. Every digest of a document containing one was unreproducible
    /// by any other implementation.
    /// </remarks>
    private static string CanonicalString(string value)
    {
        var builder = new StringBuilder(value.Length + 2).Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (character < 0x20)
                        builder.Append(CultureInfo.InvariantCulture, $"\\u{(int)character:x4}");
                    else
                        builder.Append(character);
                    break;
            }
        }
        return builder.Append('"').ToString();
    }

    /// <summary>
    /// RFC 8785 section 3.2.2.3 number serialization: ES6 <c>Number::toString</c>.
    /// An integral value has no fractional part ("1996", never "1996.0"), fractions
    /// use the shortest round-trip digits, and exponent forms appear only where ES6
    /// uses them (magnitude at or above 1e21, or below 1e-6), spelled "1e+21" and "1e-7".
    /// </summary>
    public static string CanonicalNumber(double number)
    {
        if (double.IsNaN(number) || double.IsInfinity(number))
            throw new SerializationException("APR semantic digests require finite JSON numbers.");
        if (number == 0) return "0"; // ES6 prints negative zero as "0"
        // "R" yields the shortest digit string that round-trips, which is the digit
        // sequence ES6 requires; only the placement of the point and the exponent
        // spelling differ, so those are rebuilt from the digits and exponent.
        var text = number.ToString("R", CultureInfo.InvariantCulture);
        var sign = text.StartsWith('-') ? "-" : "";
        text = text.TrimStart('-');
        var exponentIndex = text.IndexOfAny(['E', 'e']);
        var mantissa = exponentIndex < 0 ? text : text[..exponentIndex];
        var exponent = exponentIndex < 0 ? 0 : int.Parse(text[(exponentIndex + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var pointIndex = mantissa.IndexOf('.');
        var digits = pointIndex < 0 ? mantissa : mantissa.Remove(pointIndex, 1);
        var point = (pointIndex < 0 ? mantissa.Length : pointIndex) + exponent;
        var stripped = digits.TrimStart('0');
        point -= digits.Length - stripped.Length;
        digits = stripped.TrimEnd('0');
        int k = digits.Length, n = point;
        if (k <= n && n <= 21) return sign + digits + new string('0', n - k);
        if (0 < n && n <= 21) return sign + digits[..n] + "." + digits[n..];
        if (-6 < n && n <= 0) return sign + "0." + new string('0', -n) + digits;
        var exponentValue = n - 1;
        var body = k > 1 ? digits[..1] + "." + digits[1..] : digits;
        return sign + body + "e" + (exponentValue > 0 ? "+" : "-") + Math.Abs(exponentValue).ToString(CultureInfo.InvariantCulture);
    }
}

/// <summary>A non-plaintext integrity-manifest entry.</summary>
/// <param name="Path">RFC 6901 pointer; the empty string denotes the root.</param>
/// <param name="Digest">Digest of the value at <paramref name="Path"/>.</param>
public sealed record AprManifestEntry(string Path, string Digest);

/// <summary>A complete beta.6 integrity manifest.</summary>
/// <param name="Root">Digest of the complete semantic form model.</param>
/// <param name="Entries">Root and descendant value digests.</param>
public sealed record AprIntegrityManifest(string Root, IReadOnlyList<AprManifestEntry> Entries);
