using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, value);
        return stream.ToArray();
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

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String: writer.WriteStringValue(value.GetString()); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            case JsonValueKind.Number:
                if (!value.TryGetDouble(out var number))
                    throw new SerializationException("APR semantic digests require finite JSON numbers.");
                writer.WriteRawValue(CanonicalNumber(number), skipInputValidation: false);
                break;
            default: throw new SerializationException("Unsupported JSON value in APR semantic digest.");
        }
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
