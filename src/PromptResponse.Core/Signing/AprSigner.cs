using System.Security.Cryptography;
using PromptResponse.Core.Models;

namespace PromptResponse.Core.Signing;

/// <summary>
/// Produces detached APR signatures (scheme <c>apr-sig-v1</c>, ECDSA P-256 +
/// SHA-256). A publisher signs the template (binding the submission URL); a
/// filler signs the responses in a scope. The returned <see cref="Signature"/>
/// is added to <see cref="AprDocument.Signatures"/> by the caller.
/// </summary>
public static class AprSigner
{
    /// <summary>
    /// Signs the form definition as the publisher, binding the submission URL so
    /// it cannot be altered without invalidating the signature.
    /// </summary>
    public static Signature SignTemplate(
        AprDocument document,
        ECDsa privateKey,
        string signerName,
        string? identifier,
        string? submissionUrl,
        DateTime signedAtUtc,
        string id = "publisher")
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(privateKey);

        var signer = new Signer
        {
            Name = signerName,
            Identifier = identifier,
            PublicKey = SignatureKeys.ExportPublicKeyPem(privateKey),
        };
        var signedAt = signedAtUtc.ToUniversalTime().ToString("o");
        var payload = AprCanonicalizer.PublisherPayload(document, signer, submissionUrl, signedAt);
        var value = privateKey.SignData(payload, HashAlgorithmName.SHA256);

        return new Signature
        {
            Id = id,
            Role = SignatureRole.Publisher,
            Signer = signer,
            Scope = "template",
            SubmissionUrl = submissionUrl,
            Algorithm = "ecdsa-p256-sha256",
            Canonicalization = AprCanonicalizer.Scheme,
            SignedAt = signedAt,
            Value = Convert.ToBase64String(value),
        };
    }

    /// <summary>
    /// Signs the responses of <paramref name="fields"/> as a filler. Multiple
    /// fillers can each sign their own scope; editing a covered field invalidates
    /// only the signatures that cover it.
    /// </summary>
    public static Signature SignFields(
        AprDocument document,
        ECDsa privateKey,
        string signerName,
        string? identifier,
        IEnumerable<string> fields,
        DateTime signedAtUtc,
        string id)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(privateKey);
        ArgumentNullException.ThrowIfNull(fields);

        var signer = new Signer
        {
            Name = signerName,
            Identifier = identifier,
            PublicKey = SignatureKeys.ExportPublicKeyPem(privateKey),
        };
        var signedAt = signedAtUtc.ToUniversalTime().ToString("o");
        var fieldList = fields.Distinct(StringComparer.Ordinal).ToList();
        var payload = AprCanonicalizer.FillerPayload(document, fieldList, signer, signedAt);
        var value = privateKey.SignData(payload, HashAlgorithmName.SHA256);

        return new Signature
        {
            Id = id,
            Role = SignatureRole.Filler,
            Signer = signer,
            Scope = "fields",
            Fields = fieldList,
            Algorithm = "ecdsa-p256-sha256",
            Canonicalization = AprCanonicalizer.Scheme,
            SignedAt = signedAt,
            Value = Convert.ToBase64String(value),
        };
    }
}
