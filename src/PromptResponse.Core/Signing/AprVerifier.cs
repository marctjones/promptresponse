using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using PromptResponse.Core.Models;

namespace PromptResponse.Core.Signing;

/// <summary>How far a verified signature's certificate can be trusted.</summary>
public enum SignatureTrust
{
    /// <summary>Certificate chains to a configured trusted root, or is a pinned self-signed cert.</summary>
    Trusted,

    /// <summary>Valid signature from a self-signed certificate that is not pinned — identity unverified.</summary>
    SelfSigned,

    /// <summary>Valid signature, but the (CA-issued) certificate does not chain to a trusted root.</summary>
    Untrusted,

    /// <summary>The signature does not verify — the covered content was altered, or it is malformed.</summary>
    Invalid,
}

/// <summary>Trust configuration for verification.</summary>
public sealed class AprTrustOptions
{
    /// <summary>
    /// Trusted roots / pinned certificates. CA-issued signer certs must chain to one
    /// of these; a self-signed signer cert is trusted only if it is pinned here.
    /// When empty, the OS trust store is used for CA chains and self-signed certs
    /// report <see cref="SignatureTrust.SelfSigned"/>.
    /// </summary>
    public IReadOnlyCollection<X509Certificate2>? TrustAnchors { get; init; }

    /// <summary>Whether to check revocation (OCSP/CRL) — requires network.</summary>
    public bool CheckRevocation { get; init; }

    /// <summary>Default: OS trust store, no revocation check.</summary>
    public static AprTrustOptions Default { get; } = new();
}

/// <summary>The result of verifying one <see cref="Signature"/>.</summary>
/// <param name="Id">The signature's id.</param>
/// <param name="Role">Publisher or filler.</param>
/// <param name="SignerName">The certificate subject's common name.</param>
/// <param name="SignerSubject">The full certificate subject DN.</param>
/// <param name="ContentValid">Whether the signature cryptographically verifies over the current content.</param>
/// <param name="Trust">How far the signer's certificate is trusted.</param>
/// <param name="Status">A human-readable explanation.</param>
public sealed record SignatureVerification(
    string Id, SignatureRole Role, string SignerName, string SignerSubject,
    bool ContentValid, SignatureTrust Trust, string Status);

/// <summary>
/// Verifies detached CMS/PKCS#7 APR signatures: recomputes the canonical payload
/// over the document's current content, checks the CMS signature, and evaluates the
/// signer certificate's trust (chain to a trusted root, or a pinned self-signed cert).
/// </summary>
public static class AprVerifier
{
    /// <summary>Verifies every signature on the document.</summary>
    public static IReadOnlyList<SignatureVerification> VerifyAll(AprDocument document, AprTrustOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Signatures is not { Count: > 0 })
        {
            return Array.Empty<SignatureVerification>();
        }
        return document.Signatures.Select(s => Verify(document, s, options)).ToList();
    }

    /// <summary>Verifies a single signature against the document's current content.</summary>
    public static SignatureVerification Verify(AprDocument document, Signature signature, AprTrustOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(signature);
        options ??= AprTrustOptions.Default;

        try
        {
            var payload = signature.Scope == "template"
                ? AprCanonicalizer.PublisherPayload(document, signature.SubmissionUrl, signature.SignedAt)
                : AprCanonicalizer.FillerPayload(document, signature.Fields, signature.SignedAt);

            var cms = new SignedCms(new ContentInfo(payload), detached: true);
            cms.Decode(Convert.FromBase64String(signature.Cms));

            try
            {
                cms.CheckSignature(verifySignatureOnly: true);
            }
            catch (CryptographicException)
            {
                return Result(signature, false, SignatureTrust.Invalid,
                    "invalid — the covered content was altered, or the signature does not verify");
            }

            var cert = cms.SignerInfos.Count > 0 ? cms.SignerInfos[0].Certificate : null;
            if (cert is null)
            {
                return Result(signature, false, SignatureTrust.Invalid, "invalid — no signer certificate present");
            }

            var (trust, status) = EvaluateTrust(cert, options);
            return new SignatureVerification(
                signature.Id, signature.Role,
                cert.GetNameInfo(X509NameType.SimpleName, forIssuer: false), cert.Subject,
                ContentValid: true, trust, status);
        }
        catch (Exception ex)
        {
            return Result(signature, false, SignatureTrust.Invalid, "error: " + ex.Message);
        }
    }

    private static (SignatureTrust Trust, string Status) EvaluateTrust(X509Certificate2 cert, AprTrustOptions options)
    {
        var selfSigned = string.Equals(cert.Subject, cert.Issuer, StringComparison.Ordinal);
        var anchors = options.TrustAnchors;

        if (selfSigned)
        {
            var pinned = anchors?.Any(a => a.RawData.AsSpan().SequenceEqual(cert.RawData)) ?? false;
            return pinned
                ? (SignatureTrust.Trusted, "trusted — pinned self-signed certificate")
                : (SignatureTrust.SelfSigned, "valid signature; self-signed certificate, identity not verified (pin the certificate to trust it)");
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = options.CheckRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck;
        if (anchors is { Count: > 0 })
        {
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            foreach (var a in anchors)
            {
                chain.ChainPolicy.CustomTrustStore.Add(a);
                chain.ChainPolicy.ExtraStore.Add(a); // so intermediates resolve
            }
        }

        if (chain.Build(cert))
        {
            return (SignatureTrust.Trusted, "trusted — certificate chains to a trusted root");
        }

        var reason = string.Join("; ", chain.ChainStatus
            .Select(s => s.StatusInformation.Trim())
            .Where(s => s.Length > 0));
        return (SignatureTrust.Untrusted,
            "valid signature, but the certificate is not trusted: " + (reason.Length > 0 ? reason : "chain could not be built"));
    }

    private static SignatureVerification Result(Signature s, bool contentValid, SignatureTrust trust, string status) =>
        new(s.Id, s.Role, s.Signer.Name, s.Signer.Subject, contentValid, trust, status);
}
