using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using PromptResponse.Core.Models;

namespace PromptResponse.Core.Signing;

/// <summary>
/// Produces detached APR signatures (scheme <c>apr-sig-v2</c>) as industry-standard
/// CMS/PKCS#7 <c>SignedData</c> over the document's canonical content, signed by an
/// X.509 certificate. A publisher signs the template (binding the submission URL);
/// a filler signs the responses in a scope. The returned <see cref="Signature"/> is
/// added to <see cref="AprDocument.Signatures"/> by the caller.
/// </summary>
public static class AprSigner
{
    // SHA-256 (FIPS 180-4).
    private static readonly System.Security.Cryptography.Oid Sha256 = new("2.16.840.1.101.3.4.2.1");

    /// <summary>
    /// Signs the form definition as the publisher, binding the submission URL so it
    /// cannot be altered without invalidating the signature.
    /// </summary>
    public static Signature SignTemplate(
        AprDocument document,
        X509Certificate2 certificate,
        DateTime signedAtUtc,
        string id = "publisher")
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(certificate);

        // Signing binds the submission URL so it cannot be redirected without breaking
        // the signature. Attesting to a URL carrying hidden characters would bind an
        // address that renders as one host and resolves as another — the very
        // substitution the binding exists to prevent. Refuse rather than clean it:
        // choosing a replacement host is the author's decision, not this library's.
        if (Text.StringSanitizer.ContainsHiddenCharacters(document.Metadata?.SubmissionUrl))
        {
            throw new InvalidOperationException(
                "Refusing to sign: the submission URL contains hidden characters (zero-width, bidi, or similar), "
                + "so it may display as a different address than it is. Retype the URL and sign again.");
        }

        var signedAt = signedAtUtc.ToUniversalTime().ToString("o");
        var payload = AprCanonicalizer.PublisherPayload(document, signedAt);

        return new Signature
        {
            Id = id,
            Role = SignatureRole.Publisher,
            Signer = SignerFrom(certificate),
            Scope = "template",
            Algorithm = AlgorithmId(certificate),
            Canonicalization = AprCanonicalizer.Scheme,
            SignedAt = signedAt,
            Cms = Convert.ToBase64String(SignDetached(payload, certificate)),
        };
    }

    /// <summary>
    /// Signs the responses of <paramref name="fields"/> as a filler. Multiple
    /// fillers can each sign their own scope; editing a covered field invalidates
    /// only the signatures that cover it.
    /// </summary>
    public static Signature SignFields(
        AprDocument document,
        X509Certificate2 certificate,
        IEnumerable<string> fields,
        DateTime signedAtUtc,
        string id)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentNullException.ThrowIfNull(fields);

        var signedAt = signedAtUtc.ToUniversalTime().ToString("o");
        var fieldList = fields.Distinct(StringComparer.Ordinal).ToList();
        var payload = AprCanonicalizer.FillerPayload(document, fieldList, signedAt);

        return new Signature
        {
            Id = id,
            Role = SignatureRole.Filler,
            Signer = SignerFrom(certificate),
            Scope = "fields",
            Fields = fieldList,
            Algorithm = AlgorithmId(certificate),
            Canonicalization = AprCanonicalizer.Scheme,
            SignedAt = signedAt,
            Cms = Convert.ToBase64String(SignDetached(payload, certificate)),
        };
    }

    private static byte[] SignDetached(byte[] content, X509Certificate2 certificate)
    {
        var cms = new SignedCms(new ContentInfo(content), detached: true);
        var signer = new CmsSigner(certificate)
        {
            // Embed the signer's certificate; the verifier supplies CA/intermediates
            // as trust anchors. Self-signed certs are pinned by the verifier.
            IncludeOption = X509IncludeOption.EndCertOnly,
            DigestAlgorithm = Sha256,
        };
        signer.SignedAttributes.Add(new Pkcs9SigningTime(DateTime.UtcNow));
        cms.ComputeSignature(signer);
        return cms.Encode();
    }

    private static Signer SignerFrom(X509Certificate2 cert) => new()
    {
        Name = cert.GetNameInfo(X509NameType.SimpleName, forIssuer: false),
        Subject = cert.Subject,
        Issuer = cert.Issuer,
        Thumbprint = SignatureCertificates.Sha256Thumbprint(cert),
        SelfSigned = string.Equals(cert.Subject, cert.Issuer, StringComparison.Ordinal),
    };

    private static string AlgorithmId(X509Certificate2 cert) =>
        cert.GetKeyAlgorithm() == "1.2.840.10045.2.1" // id-ecPublicKey
            ? "cms/ecdsa-sha256"
            : "cms/rsa-sha256";
}
