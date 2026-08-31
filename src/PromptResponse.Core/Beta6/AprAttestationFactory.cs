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
            ["version"] = "1.0-beta.6",
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
}
