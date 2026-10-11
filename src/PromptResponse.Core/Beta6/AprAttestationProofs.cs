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

    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";
    private const string P256Oid = "1.2.840.10045.3.1.7";

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
            // The type names an algorithm (APR-ATTEST-037): ECDSA over P-256 with SHA-256. A CMS
            // signature that verifies but is made another way is a real signature that is not
            // this proof type, so it does not verify as one. One signer, because the trust result
            // is about one certificate and a second signer's would otherwise go unexamined.
            if (cms.SignerInfos.Count != 1)
                return new(type, false, AprAttestationTrust.Invalid,
                    $"invalid — {type} proof carries {cms.SignerInfos.Count} signers, not exactly one");
            var signer = cms.SignerInfos[0];
            if (signer.DigestAlgorithm.Value != Sha256Oid)
                return new(type, false, AprAttestationTrust.Invalid,
                    $"invalid — signed with digest {signer.DigestAlgorithm.Value ?? "(none)"}, not SHA-256");
            var certificate = signer.Certificate;
            if (certificate is null)
                return new(type, false, AprAttestationTrust.Invalid, "invalid — CMS proof has no signer certificate");
            using (var key = certificate.GetECDsaPublicKey())
            {
                if (key is null)
                    return new(type, false, AprAttestationTrust.Invalid, "invalid — signer key is not ECDSA");
                var curve = key.ExportParameters(includePrivateParameters: false).Curve.Oid;
                if (curve.Value != P256Oid && curve.FriendlyName is not ("nistP256" or "ECDSA_P256" or "secp256r1"))
                    return new(type, false, AprAttestationTrust.Invalid, "invalid — signer key is not on the P-256 curve");
            }
            cms.CheckSignature(verifySignatureOnly: true);
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
