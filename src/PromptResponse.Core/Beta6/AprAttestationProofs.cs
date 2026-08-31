using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace PromptResponse.Core.Beta6;

/// <summary>Cryptographic and trust result for one beta.6 attestation proof.</summary>
/// <param name="Type">The proof type declared by the record.</param>
/// <param name="ContentValid">Whether CMS validates against the proof-free envelope.</param>
/// <param name="Trust">The independent certificate-trust result, when a CMS certificate is present.</param>
/// <param name="Status">Human-readable non-gating explanation.</param>
public sealed record AprAttestationProofVerification(string Type, bool ContentValid, AprAttestationTrust Trust, string Status);

/// <summary>Verifies CMS proofs over a beta.6 attestation envelope.</summary>
public static class AprAttestationProofs
{
    /// <summary>The beta.6 CMS proof identifier.</summary>
    public const string CmsEcdsaP256Sha256 = "cms/ecdsa-p256-sha256";

    /// <summary>Verifies every recognized and unsupported proof independently.</summary>
    public static IReadOnlyList<AprAttestationProofVerification> Verify(
        AprAttestationRecord attestation, AprAttestationTrustOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(attestation);
        options ??= AprAttestationTrustOptions.Default;
        if (!attestation.Value.TryGetProperty("proofs", out var proofs) || proofs.ValueKind != JsonValueKind.Array)
            return [];
        var payload = SigningPayload(attestation);
        return proofs.EnumerateArray().Select(proof => VerifyOne(proof, payload, options)).ToList();
    }

    /// <summary>Returns the canonical proof-free envelope that a CMS proof signs.</summary>
    public static byte[] SigningPayload(AprAttestationRecord attestation)
    {
        ArgumentNullException.ThrowIfNull(attestation);
        return AprSemanticDigest.Canonicalize(EnvelopeWithoutProofs(attestation.Value));
    }

    private static AprAttestationProofVerification VerifyOne(JsonElement proof, byte[] payload, AprAttestationTrustOptions options)
    {
        var type = proof.TryGetProperty("type", out var typeNode) ? typeNode.GetString() ?? "" : "";
        if (type != CmsEcdsaP256Sha256)
            return new(type, false, AprAttestationTrust.Invalid, "unverifiable — unsupported attestation proof type");
        try
        {
            var encoded = proof.GetProperty("value").GetString();
            if (string.IsNullOrWhiteSpace(encoded))
                return new(type, false, AprAttestationTrust.Invalid, "invalid — empty CMS proof");
            var cms = new SignedCms(new ContentInfo(payload), detached: true);
            cms.Decode(Convert.FromBase64String(encoded));
            cms.CheckSignature(verifySignatureOnly: true);
            var certificate = cms.SignerInfos.Count > 0 ? cms.SignerInfos[0].Certificate : null;
            if (certificate is null)
                return new(type, false, AprAttestationTrust.Invalid, "invalid — CMS proof has no signer certificate");
            var (trust, status) = AprAttestationTrustEvaluator.Evaluate(certificate, options);
            return new(type, true, trust, status);
        }
        catch (CryptographicException)
        {
            return new(type, false, AprAttestationTrust.Invalid, "invalid — CMS proof does not verify over this attestation");
        }
        catch (Exception ex)
        {
            return new(type, false, AprAttestationTrust.Invalid, "invalid — " + ex.Message);
        }
    }

    private static JsonElement EnvelopeWithoutProofs(JsonElement attestation)
    {
        var values = attestation.EnumerateObject()
            .Where(property => property.Name != "proofs")
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
        using var document = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(values));
        return document.RootElement.Clone();
    }
}
