using System.Security.Cryptography.X509Certificates;
using PromptResponse.Core.Models;

namespace PromptResponse.Core.Services.Certificates;

/// <summary>
/// Service for generating digital certificates compatible with commercial CAs,
/// Microsoft Certificate Services, Adobe Sign, and DocuSign.
/// </summary>
public interface ICertificateGenerator
{
    /// <summary>
    /// Generate a self-signed certificate for testing or personal use.
    /// The generated certificate will be structurally compatible with commercial CA certificates
    /// (same format, proper X.509 extensions) but won't be automatically trusted until
    /// added to the system's trusted root store.
    /// </summary>
    /// <param name="request">Certificate request with subject information and usage flags</param>
    /// <returns>X.509 certificate with private key</returns>
    X509Certificate2 GenerateSelfSignedCertificate(CertificateRequest request);

    /// <summary>
    /// Generate a test Certificate Authority (CA) certificate.
    /// This can be used to issue test certificates that simulate a commercial CA hierarchy.
    /// </summary>
    /// <param name="caName">Name for the CA (e.g., "Test CA")</param>
    /// <returns>CA certificate with private key</returns>
    X509Certificate2 GenerateTestCA(string caName);

    /// <summary>
    /// Generate a certificate signed by a test CA.
    /// This simulates how commercial CAs issue certificates, creating a proper trust chain.
    /// </summary>
    /// <param name="request">Certificate request with subject information</param>
    /// <param name="caCert">CA certificate to sign with (must have private key)</param>
    /// <returns>Certificate signed by the CA with private key</returns>
    X509Certificate2 GenerateTestCertificate(CertificateRequest request, X509Certificate2 caCert);

    /// <summary>
    /// Export certificate with private key in PKCS#12 (.pfx) format.
    /// This format is compatible with Windows, macOS, Linux, 1Password, and most certificate stores.
    /// </summary>
    /// <param name="cert">Certificate to export (must have private key)</param>
    /// <param name="password">Password to protect the private key</param>
    /// <returns>PKCS#12 formatted data</returns>
    byte[] ExportPfx(X509Certificate2 cert, string password);
}
