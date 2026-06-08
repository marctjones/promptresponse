using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PromptResponse.Core.Signing;

/// <summary>
/// Helpers for the X.509 certificates used by APR signing. Uses only built-in
/// .NET crypto. Supports both trust models the project targets:
/// <list type="bullet">
///   <item><b>Self-signed</b> — for organizations without a CA, or for testing;
///   trust is established by pinning the certificate.</item>
///   <item><b>CA-issued</b> — verified by chaining to a trusted root (Federal PKI,
///   an org CA, eIDAS, …). Load real certs (incl. PIV/CAC smartcards) from a
///   <c>.pfx</c> or the OS certificate store.</item>
/// </list>
/// Defaults to ECDSA P-256 + SHA-256 (FIPS 186-5 / matches PIV).
/// </summary>
public static class SignatureCertificates
{
    /// <summary>
    /// Creates a self-signed signing certificate (ECDSA P-256) with a private key,
    /// suitable for signing. Pin it (by thumbprint) to establish trust.
    /// </summary>
    public static X509Certificate2 CreateSelfSigned(string subjectName, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={subjectName}", ec, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, critical: true));
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        return request.CreateSelfSigned(notBefore, notAfter);
    }

    /// <summary>Creates a self-signed CA certificate (for issuing leaf certs; mainly for tests).</summary>
    public static X509Certificate2 CreateCertificateAuthority(string subjectName, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={subjectName}", ec, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));
        return request.CreateSelfSigned(notBefore, notAfter);
    }

    /// <summary>Issues a signing leaf certificate signed by <paramref name="issuer"/> (a CA).</summary>
    public static X509Certificate2 IssueSigningCertificate(
        X509Certificate2 issuer, string subjectName, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={subjectName}", leafKey, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, critical: true));
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

        var serial = new byte[8];
        RandomNumberGenerator.Fill(serial);
        using var issued = request.Create(issuer, notBefore, notAfter, serial);
        // Re-attach the leaf's private key so the result can sign.
        return issued.CopyWithPrivateKey(leafKey);
    }

    /// <summary>Loads a certificate (with private key) from a PKCS#12 / .pfx file.</summary>
    public static X509Certificate2 LoadPfx(string path, string? password = null) =>
        X509CertificateLoader.LoadPkcs12FromFile(path, password);

    /// <summary>The certificate's SHA-256 thumbprint as an uppercase hex string (for pinning).</summary>
    public static string Sha256Thumbprint(X509Certificate2 cert) =>
        Convert.ToHexString(SHA256.HashData(cert.RawData));
}
