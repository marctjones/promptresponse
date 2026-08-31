using System.Text;
using System.Text.Json;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using YamlDotNet.Serialization;

namespace PromptResponse.Core.Beta6;

/// <summary>Source representation for the beta.6 semantic model.</summary>
public enum AprRepresentation
{
    /// <summary>APR JSON with comments and trailing commas.</summary>
    Jsonc,
    /// <summary>Restricted APR YAML.</summary>
    Yaml,
}

/// <summary>Raised when a caller asks a single-form API to select from a stream.</summary>
public sealed class AprStreamRequiresIterationException : SerializationException
{
    /// <summary>Initializes the explicit single-form stream error.</summary>
    public AprStreamRequiresIterationException()
        : base("APR_STREAM_REQUIRES_ITERATION: iterate stream records instead of selecting one by position.") { }
}

/// <summary>
/// One independent beta.6 stream record. An attestation is deliberately opaque at
/// this layer; core+streams must carry it without making an integrity assertion.
/// </summary>
public abstract record AprStreamRecord;
/// <summary>An independent complete form occurrence.</summary>
/// <param name="Form">The parsed form semantic model.</param>
/// <param name="Value">The complete form semantic model, including extensions.</param>
public sealed record AprFormRecord(AprDocument Form, JsonElement Value) : AprStreamRecord;
/// <summary>An independent attestation occurrence, preserved as opaque JSON.</summary>
/// <param name="Value">The complete attestation semantic model.</param>
public sealed record AprAttestationRecord(JsonElement Value) : AprStreamRecord;

/// <summary>
/// Representation and stream boundary for APR beta.6. It normalizes JSONC/YAML
/// source to the existing semantic form model and never infers record relations
/// from a stream's physical order.
/// </summary>
public sealed class AprBeta6Reader
{
    private const string Beta6 = "1.0-beta.6";
    private readonly AprJsonSerializer _forms = new();
    private readonly IDeserializer _yaml = new DeserializerBuilder().Build();
    private readonly ISerializer _yamlWriter = new SerializerBuilder().Build();

    /// <summary>Reads exactly one form; streams require explicit iteration.</summary>
    public AprDocument ReadForm(string source, AprRepresentation representation)
    {
        var records = ReadStream(source, representation);
        if (records.Count != 1 || records[0] is not AprFormRecord form)
            throw new AprStreamRequiresIterationException();
        return form.Form;
    }

    /// <summary>Reads all independent records without deduplicating forms.</summary>
    public IReadOnlyList<AprStreamRecord> ReadStream(string source, AprRepresentation representation)
    {
        ArgumentNullException.ThrowIfNull(source);
        var records = representation switch
        {
            AprRepresentation.Jsonc => SplitJsoncRecords(source),
            AprRepresentation.Yaml => SplitYamlDocuments(source),
            _ => throw new ArgumentOutOfRangeException(nameof(representation)),
        };
        return records.Select(record => ParseRecord(record, representation)).ToList();
    }

    /// <summary>Writes one beta.6 form in the requested source representation.</summary>
    public string WriteForm(AprDocument form, AprRepresentation representation)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (form.Version != Beta6)
            throw new SerializationException("APR beta.6 writers require version '1.0-beta.6'.");
        return WriteJson(_forms.Serialize(form), representation);
    }

    /// <summary>Writes all independent stream records in their original occurrence order.</summary>
    public string WriteStream(IEnumerable<AprStreamRecord> records, AprRepresentation representation)
    {
        ArgumentNullException.ThrowIfNull(records);
        var encoded = records.Select(record => record switch
        {
            // Keep the full parsed semantic value (including unknown extensions)
            // rather than regenerating it from the typed model. A stream rewrite
            // must not invalidate an attestation over its subject form.
            AprFormRecord form when form.Value.ValueKind == JsonValueKind.Undefined => WriteForm(form.Form, representation),
            AprFormRecord form => WriteJson(ValidatedFormRecordJson(form), representation),
            AprAttestationRecord attestation => WriteJson(attestation.Value.GetRawText(), representation),
            _ => throw new ArgumentException("Unknown APR beta.6 stream record.", nameof(records)),
        }).ToList();
        return representation == AprRepresentation.Jsonc
            ? string.Concat(encoded.Select(record => "\u001e" + record + "\n"))
            : string.Join("---\n", encoded);
    }

    private AprStreamRecord ParseRecord(string source, AprRepresentation representation)
    {
        var json = representation == AprRepresentation.Jsonc
            ? StripJsonc(source)
            : JsonSerializer.Serialize(_yaml.Deserialize<object>(source));
        if (representation == AprRepresentation.Jsonc) EnsureUniqueObjectMembers(json);
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new SerializationException("An APR beta.6 record must be an object.");
        if (root.TryGetProperty("recordType", out var kind))
        {
            if (kind.ValueKind != JsonValueKind.String || kind.GetString() != "attestation")
                throw new SerializationException("Unknown APR beta.6 stream record type.");
            RequireBeta6(root);
            ValidateAttestation(root);
            return new AprAttestationRecord(root.Clone());
        }
        RequireBeta6(root);
        if (root.TryGetProperty("signatures", out _))
            throw new SerializationException("RETIRED_EMBEDDED_SIGNATURES: beta.6 forms carry attestations as stream records.");
        return new AprFormRecord(_forms.Deserialize(root.GetRawText()), root.Clone());
    }

    private static void RequireBeta6(JsonElement root)
    {
        if (!root.TryGetProperty("version", out var version) || version.GetString() != Beta6)
            throw new SerializationException("APR beta.6 records must declare version '1.0-beta.6'.");
    }

    private static void ValidateAttestation(JsonElement value)
    {
        RequireObject(value, "subject", out var subject);
        RequireDigest(subject, "digest", "subject.digest");
        if (!subject.TryGetProperty("canonicalization", out var canonicalization) || canonicalization.GetString() != AprSemanticDigest.Canonicalization)
            throw new SerializationException("beta.6 attestation subject.canonicalization must be jcs-sha256.");
        RequireObject(value, "scope", out var scope);
        if (!scope.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String || (kind.GetString() is not ("document" or "fields")))
            throw new SerializationException("beta.6 attestation scope.kind must be document or fields.");
        if (kind.GetString() == "fields")
        {
            if (!scope.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Array || fields.GetArrayLength() == 0 || fields.EnumerateArray().Any(field => field.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(field.GetString())))
                throw new SerializationException("beta.6 fields attestations require non-blank scope.fields.");
        }
        RequireObject(value, "manifest", out var manifest);
        RequireDigest(manifest, "root", "manifest.root");
        if (!manifest.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            throw new SerializationException("beta.6 attestation manifest.entries must be an array.");
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("path", out var path) || path.ValueKind != JsonValueKind.String)
                throw new SerializationException("beta.6 attestation manifest entries require a string path.");
            RequireDigest(entry, "digest", "manifest.entries[].digest");
        }
        if (!value.TryGetProperty("proofs", out var proofs) || proofs.ValueKind != JsonValueKind.Array || !value.TryGetProperty("witnesses", out var witnesses) || witnesses.ValueKind != JsonValueKind.Array)
            throw new SerializationException("beta.6 attestations require proofs and witnesses arrays.");
        foreach (var witness in witnesses.EnumerateArray())
        {
            if (witness.ValueKind != JsonValueKind.String || !IsDigest(witness.GetString()))
                throw new SerializationException("beta.6 attestation witnesses must be sha256 digests.");
        }
    }

    private static void RequireObject(JsonElement parent, string name, out JsonElement value)
    {
        if (!parent.TryGetProperty(name, out value) || value.ValueKind != JsonValueKind.Object)
            throw new SerializationException($"beta.6 attestation {name} must be an object.");
    }

    private static void RequireDigest(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || !IsDigest(value.GetString()))
            throw new SerializationException($"beta.6 attestation {path} must be a lowercase sha256 digest.");
    }

    private static bool IsDigest(string? value) =>
        value is { Length: 71 } && value.StartsWith("sha256:", StringComparison.Ordinal) &&
        value[7..].All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string ValidatedFormRecordJson(AprFormRecord form)
    {
        RequireBeta6(form.Value);
        if (form.Value.TryGetProperty("signatures", out _))
            throw new SerializationException("RETIRED_EMBEDDED_SIGNATURES: beta.6 forms cannot emit root signatures.");
        return form.Value.GetRawText();
    }

    private static IReadOnlyList<string> SplitJsoncRecords(string source)
    {
        if (!source.Contains('\u001e')) return [source];
        var records = source.Split('\u001e', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (records.Length == 0) throw new SerializationException("An APR JSONC stream has no records.");
        return records;
    }

    private static IReadOnlyList<string> SplitYamlDocuments(string source)
    {
        RejectYamlFeatures(source);
        var documents = System.Text.RegularExpressions.Regex.Split(source, "(?m)^---\\s*$")
            .Where(document => !string.IsNullOrWhiteSpace(document)).ToArray();
        return documents.Length == 0 ? [source] : documents;
    }

    private static void RejectYamlFeatures(string source)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(source, @"(?m)(?:^|[\s\[{,])(?:[&*!]|<<\s*:)") )
            throw new SerializationException("APR YAML forbids anchors, aliases, tags, and merge keys.");
    }

    private string WriteJson(string json, AprRepresentation representation) => representation switch
    {
        // Strict JSON is valid JSONC and is the canonical writer output.
        AprRepresentation.Jsonc => json,
        AprRepresentation.Yaml => _yamlWriter.Serialize(_yaml.Deserialize<object>(json)),
        _ => throw new ArgumentOutOfRangeException(nameof(representation)),
    };

    // JSONC is normalized before System.Text.Json sees it. The scanner is string-aware,
    // so URLs and quoted comment-looking text remain data.
    private static string StripJsonc(string input)
    {
        var withoutComments = new StringBuilder(input.Length);
        var quote = false; var escaped = false;
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (quote)
            {
                withoutComments.Append(c);
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') quote = false;
                continue;
            }
            if (c == '"') { quote = true; withoutComments.Append(c); continue; }
            if (c == '/' && i + 1 < input.Length && input[i + 1] == '/')
            {
                while (i < input.Length && input[i] != '\n') i++;
                if (i < input.Length) withoutComments.Append('\n');
                continue;
            }
            if (c == '/' && i + 1 < input.Length && input[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < input.Length && !(input[i] == '*' && input[i + 1] == '/')) i++;
                if (i + 1 >= input.Length) throw new SerializationException("Unterminated JSONC block comment.");
                i++;
                continue;
            }
            withoutComments.Append(c);
        }
        var output = new StringBuilder(withoutComments.Length);
        quote = false; escaped = false;
        for (var i = 0; i < withoutComments.Length; i++)
        {
            var c = withoutComments[i];
            if (quote)
            {
                output.Append(c);
                if (escaped) escaped = false; else if (c == '\\') escaped = true; else if (c == '"') quote = false;
                continue;
            }
            if (c == '"') { quote = true; output.Append(c); continue; }
            if (c == ',')
            {
                var next = i + 1;
                while (next < withoutComments.Length && char.IsWhiteSpace(withoutComments[next])) next++;
                if (next < withoutComments.Length && (withoutComments[next] == '}' || withoutComments[next] == ']')) continue;
            }
            output.Append(c);
        }
        return output.ToString();
    }

    private static void EnsureUniqueObjectMembers(string json)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
        var objects = new Stack<HashSet<string>>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                objects.Push(new HashSet<string>(StringComparer.Ordinal));
            }
            else if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var name = reader.GetString()!;
                if (!objects.Peek().Add(name))
                    throw new SerializationException($"APR JSONC object has duplicate member '{name}'.");
            }
            else if (reader.TokenType == JsonTokenType.EndObject)
            {
                objects.Pop();
            }
        }
    }
}
