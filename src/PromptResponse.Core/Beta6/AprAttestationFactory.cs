using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PromptResponse.Core.Beta6;

/// <summary>Creates independent beta.6 attestations without modifying a form.</summary>
public static class AprAttestationFactory
{
    /// <summary>
    /// Creates a detached ECDSA P-256 CMS attestation for a complete beta.6 form
    /// semantic model. Certificate trust remains a verifier policy decision.
    /// </summary>
    public static AprAttestationRecord Create(
        JsonElement form,
        X509Certificate2 certificate,
        IReadOnlyList<string>? fields = null)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        if (!certificate.HasPrivateKey)
            throw new ArgumentException("The attestation certificate must include a private key.", nameof(certificate));
        if (certificate.GetECDsaPrivateKey() is null)
            throw new ArgumentException("APR beta.6 CMS attestations require an ECDSA P-256 private key.", nameof(certificate));
        RefuseHiddenCharactersInSubmissionUrls(form);

        var unsigned = CreateUnsigned(form, fields);
        var payload = AprAttestationProofs.SigningPayload(unsigned);
        var signed = new SignedCms(new ContentInfo(payload), detached: true);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate)
        {
            IncludeOption = X509IncludeOption.WholeChain,
        };
        signed.ComputeSignature(signer);

        var envelope = JsonNode.Parse(unsigned.Value.GetRawText())!.AsObject();
        envelope["proofs"] = new JsonArray(new JsonObject
        {
            ["type"] = AprAttestationProofs.CmsEcdsaP256Sha256,
            ["value"] = Convert.ToBase64String(signed.Encode()),
        });
        return Record(envelope);
    }

    /// <summary>Creates the proof-free envelope used as the signed CMS payload.</summary>
    public static AprAttestationRecord CreateUnsigned(JsonElement form, IReadOnlyList<string>? fields = null)
    {
        var manifest = AprSemanticDigest.CreateManifest(form);
        var entries = new JsonArray();
        foreach (var entry in manifest.Entries)
        {
            entries.Add(new JsonObject { ["path"] = entry.Path, ["digest"] = entry.Digest });
        }

        var scope = fields is { Count: > 0 }
            ? new JsonObject { ["kind"] = "fields", ["fields"] = new JsonArray(fields.Select(field => JsonValue.Create(field)).ToArray()) }
            : new JsonObject { ["kind"] = "document" };
        return Record(new JsonObject
        {
            ["recordType"] = "attestation",
            ["aprVersion"] = "1.0-beta.6",
            ["subject"] = new JsonObject
            {
                ["digest"] = manifest.Root,
                ["canonicalization"] = AprSemanticDigest.Canonicalization,
            },
            ["scope"] = scope,
            ["manifest"] = new JsonObject { ["root"] = manifest.Root, ["entries"] = entries },
            ["proofs"] = new JsonArray(),
            ["witnesses"] = new JsonArray(),
        });
    }

    private static AprAttestationRecord Record(JsonObject envelope)
    {
        using var document = JsonDocument.Parse(envelope.ToJsonString());
        return new AprAttestationRecord(document.RootElement.Clone());
    }

    /// <summary>
    /// A submission URL is bound into the manifest this attestation signs, so a
    /// signer who cannot see a hidden character in it would be attesting to a
    /// target that is not the one displayed to them. Unlike the human-facing
    /// text floor (title, label, description), which only ever warns, this is an
    /// identifier used in a trust decision -- the reject side of the distinction
    /// discussed for #410's follow-up: identifiers get held to a stricter line
    /// than prose a person reads once.
    /// </summary>
    private static void RefuseHiddenCharactersInSubmissionUrls(JsonElement form)
    {
        if (form.ValueKind != JsonValueKind.Object
            || !form.TryGetProperty("metadata", out var metadata)
            || metadata.ValueKind != JsonValueKind.Object
            || !metadata.TryGetProperty("submissionUrls", out var urls)
            || urls.ValueKind != JsonValueKind.Array)
        {
            return;
        }
        foreach (var url in urls.EnumerateArray())
        {
            if (url.ValueKind == JsonValueKind.String
                && Text.StringSanitizer.ContainsHiddenCharacters(url.GetString()))
            {
                throw new InvalidOperationException(
                    "Refusing to sign: metadata.submissionUrls contains a hidden character "
                    + "(zero-width, bidi, or similar). It may display as a different address "
                    + "than it actually is. Retype it rather than editing it, then attest again.");
            }
        }
    }
}
