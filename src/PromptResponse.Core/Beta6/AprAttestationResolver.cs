using System.Text.Json;

namespace PromptResponse.Core.Beta6;

/// <summary>Non-gating result state for an independently carried attestation.</summary>
public enum AprAttestationState
{
    /// <summary>The subject, manifest, and at least one supported proof verify.</summary>
    Valid,
    /// <summary>The attestation subject has not appeared in the supplied stream.</summary>
    Unresolved,
    /// <summary>The subject or manifest differs from the attestation assertion.</summary>
    Invalid,
    /// <summary>The subject and manifest match but no supported proof was verified.</summary>
    Unverifiable,
}

/// <summary>Resolution result for one independent attestation record.</summary>
/// <param name="Attestation">The original opaque record.</param>
/// <param name="State">The non-gating resolution result.</param>
/// <param name="DifferingPaths">Manifest paths whose present digest differs.</param>
/// <param name="WitnessesResolved">Number of referenced attestation envelopes present in the stream.</param>
public sealed record AprAttestationResolution(
    AprAttestationRecord Attestation,
    AprAttestationState State,
    IReadOnlyList<string> DifferingPaths,
    int WitnessesResolved);

/// <summary>Resolves beta.6 attestations by semantic digest, never by stream position.</summary>
public static class AprAttestationResolver
{
    /// <summary>Resolves every attestation against all independent form occurrences.</summary>
    public static IReadOnlyList<AprAttestationResolution> Resolve(IReadOnlyList<AprStreamRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var forms = records.OfType<AprFormRecord>().ToLookup(record => AprSemanticDigest.Digest(record.Value), StringComparer.Ordinal);
        var attestations = records.OfType<AprAttestationRecord>().ToList();
        var envelopeDigests = attestations.ToLookup(EnvelopeDigest, StringComparer.Ordinal);
        return attestations.Select(attestation => ResolveOne(attestation, forms, envelopeDigests)).ToList();
    }

    private static AprAttestationResolution ResolveOne(
        AprAttestationRecord attestation,
        ILookup<string, AprFormRecord> forms,
        ILookup<string, AprAttestationRecord> envelopes)
    {
        var value = attestation.Value;
        var subject = value.GetProperty("subject").GetProperty("digest").GetString()!;
        var witnesses = value.TryGetProperty("witnesses", out var listed) && listed.ValueKind == JsonValueKind.Array
            ? listed.EnumerateArray().Count(item => item.ValueKind == JsonValueKind.String && envelopes.Contains(item.GetString()!)) : 0;
        var form = forms[subject].FirstOrDefault();
        if (form is null)
            return new AprAttestationResolution(attestation, AprAttestationState.Unresolved, [], witnesses);

        var actual = AprSemanticDigest.CreateManifest(form.Value);
        var asserted = value.GetProperty("manifest");
        var differing = new List<string>();
        if (!asserted.TryGetProperty("root", out var root) || root.GetString() != actual.Root) differing.Add("");
        if (asserted.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            var actualByPath = actual.Entries.ToDictionary(entry => entry.Path, StringComparer.Ordinal);
            var assertedPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries.EnumerateArray())
            {
                var path = entry.GetProperty("path").GetString()!;
                assertedPaths.Add(path);
                var digest = entry.GetProperty("digest").GetString();
                if (!actualByPath.TryGetValue(path, out var current) || current.Digest != digest) differing.Add(path);
            }
            ValidateFieldsScope(form.Value, value, assertedPaths, differing);
        }
        else ValidateFieldsScope(form.Value, value, new HashSet<string>(StringComparer.Ordinal), differing);
        if (differing.Count > 0)
            return new AprAttestationResolution(attestation, AprAttestationState.Invalid,
                differing.Distinct(StringComparer.Ordinal).ToList(), witnesses);

        var proofs = AprAttestationProofs.Verify(attestation);
        var state = proofs.Any(proof => proof.ContentValid) ? AprAttestationState.Valid
            : proofs.Any(proof => proof.Status.StartsWith("invalid", StringComparison.Ordinal)) ? AprAttestationState.Invalid
            : AprAttestationState.Unverifiable;
        return new AprAttestationResolution(attestation, state, [], witnesses);
    }

    private static string EnvelopeDigest(AprAttestationRecord attestation)
    {
        using var document = JsonDocument.Parse(attestation.Value.GetRawText());
        var properties = document.RootElement.EnumerateObject()
            .Where(property => property.Name != "proofs")
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(properties);
        using var envelope = JsonDocument.Parse(bytes);
        return AprSemanticDigest.Digest(envelope.RootElement);
    }

    private static void ValidateFieldsScope(
        JsonElement form,
        JsonElement attestation,
        IReadOnlySet<string> assertedPaths,
        List<string> differing)
    {
        if (!attestation.TryGetProperty("scope", out var scope) ||
            !scope.TryGetProperty("kind", out var kind) || kind.GetString() != "fields") return;
        if (!scope.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Array || fields.GetArrayLength() == 0)
        {
            differing.Add("/scope/fields");
            return;
        }
        foreach (var field in fields.EnumerateArray())
        {
            if (field.ValueKind != JsonValueKind.String || !TryFindPrompt(form, field.GetString()!, out var promptPath, out var sections))
            {
                differing.Add("/scope/fields");
                continue;
            }
            Require(assertedPaths, promptPath, differing);
            Require(assertedPaths, promptPath + "/response", differing);
            if (PointerExists(form, promptPath + "/hints")) Require(assertedPaths, promptPath + "/hints", differing);
            foreach (var sectionPath in sections)
            {
                Require(assertedPaths, sectionPath + "/id", differing);
                Require(assertedPaths, sectionPath + "/title", differing);
                foreach (var optional in new[] { "description", "kind", "role" })
                    if (PointerExists(form, sectionPath + "/" + optional)) Require(assertedPaths, sectionPath + "/" + optional, differing);
            }
        }
    }

    private static void Require(IReadOnlySet<string> paths, string path, List<string> differing)
    {
        if (!paths.Contains(path)) differing.Add(path);
    }

    private static bool TryFindPrompt(JsonElement form, string id, out string promptPath, out List<string> sections)
    {
        sections = [];
        promptPath = string.Empty;
        return form.TryGetProperty("sections", out var roots) && FindInSections(roots, id, "/sections", sections, out promptPath);
    }

    private static bool FindInSections(JsonElement source, string id, string basePath, List<string> ancestors, out string promptPath)
    {
        var sectionIndex = 0;
        foreach (var section in source.EnumerateArray())
        {
            var sectionPath = basePath + "/" + sectionIndex++;
            ancestors.Add(sectionPath);
            if (section.TryGetProperty("prompts", out var prompts))
            {
                var promptIndex = 0;
                foreach (var prompt in prompts.EnumerateArray())
                {
                    if (prompt.TryGetProperty("id", out var promptId) && promptId.GetString() == id)
                    {
                        promptPath = sectionPath + "/prompts/" + promptIndex;
                        return true;
                    }
                    promptIndex++;
                }
            }
            if (section.TryGetProperty("sections", out var children) && FindInSections(children, id, sectionPath + "/sections", ancestors, out promptPath)) return true;
            ancestors.RemoveAt(ancestors.Count - 1);
        }
        promptPath = string.Empty;
        return false;
    }

    private static bool PointerExists(JsonElement value, string pointer)
    {
        var current = value;
        foreach (var token in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = token.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(name, out var property)) current = property;
            else if (current.ValueKind == JsonValueKind.Array && int.TryParse(name, out var index) && index >= 0 && index < current.GetArrayLength()) current = current[index];
            else return false;
        }
        return true;
    }
}
