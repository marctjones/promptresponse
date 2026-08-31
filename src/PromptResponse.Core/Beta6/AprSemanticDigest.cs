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
                if (!value.TryGetDouble(out var number) || double.IsNaN(number) || double.IsInfinity(number))
                    throw new SerializationException("APR semantic digests require finite JSON numbers.");
                writer.WriteRawValue(number.ToString("R", CultureInfo.InvariantCulture), skipInputValidation: false);
                break;
            default: throw new SerializationException("Unsupported JSON value in APR semantic digest.");
        }
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
