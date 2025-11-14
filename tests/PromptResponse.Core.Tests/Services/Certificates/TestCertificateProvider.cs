using System.Security.Cryptography.X509Certificates;
using PromptResponse.Core.Models;
using PromptResponse.Core.Services.Certificates;

namespace PromptResponse.Core.Tests.Services.Certificates;

/// <summary>
/// Provides pre-generated test certificates for automated testing.
/// Certificates are cached to improve test performance.
/// </summary>
public static class TestCertificateProvider
{
    private static X509Certificate2? _cachedEmailCert;
    private static X509Certificate2? _cachedDocumentCert;
    private static X509Certificate2? _cachedAllPurposeCert;
    private static X509Certificate2? _cachedTestCA;
    private static X509Certificate2? _cachedCASignedCert;
    private static readonly object _lock = new();

    /// <summary>
    /// Get a test certificate for email signing (S/MIME).
    /// Cached for performance.
    /// </summary>
    public static X509Certificate2 GetEmailSigningCertificate()
    {
        if (_cachedEmailCert == null)
        {
            lock (_lock)
            {
                _cachedEmailCert ??= GenerateCertificate(
                    "Test Email User",
                    "test@example.com",
                    CertificateUsage.EmailSigning);
            }
        }
        return _cachedEmailCert;
    }

    /// <summary>
    /// Get a test certificate for document signing (PDF, Word).
    /// Cached for performance.
    /// </summary>
    public static X509Certificate2 GetDocumentSigningCertificate()
    {
        if (_cachedDocumentCert == null)
        {
            lock (_lock)
            {
                _cachedDocumentCert ??= GenerateCertificate(
                    "Test Document Signer",
                    "signer@example.com",
                    CertificateUsage.DocumentSigning);
            }
        }
        return _cachedDocumentCert;
    }

    /// <summary>
    /// Get a test certificate for all purposes (email, document, code signing).
    /// Cached for performance.
    /// </summary>
    public static X509Certificate2 GetAllPurposeCertificate()
    {
        if (_cachedAllPurposeCert == null)
        {
            lock (_lock)
            {
                _cachedAllPurposeCert ??= GenerateCertificate(
                    "Test All-Purpose User",
                    "allpurpose@example.com",
                    CertificateUsage.All);
            }
        }
        return _cachedAllPurposeCert;
    }

    /// <summary>
    /// Get a test Certificate Authority for testing certificate chains.
    /// Cached for performance.
    /// </summary>
    public static X509Certificate2 GetTestCA()
    {
        if (_cachedTestCA == null)
        {
            lock (_lock)
            {
                if (_cachedTestCA == null)
                {
                    var generator = new CertificateGenerator();
                    _cachedTestCA = generator.GenerateTestCA("PromptResponse Test CA");
                }
            }
        }
        return _cachedTestCA;
    }

    /// <summary>
    /// Get a test certificate signed by the test CA (simulates commercial CA).
    /// Cached for performance.
    /// </summary>
    public static X509Certificate2 GetCASignedCertificate()
    {
        if (_cachedCASignedCert == null)
        {
            lock (_lock)
            {
                if (_cachedCASignedCert == null)
                {
                    var generator = new CertificateGenerator();
                    var ca = GetTestCA();
                    var request = new CertificateRequest
                    {
                        CommonName = "Test CA-Signed User",
                        Email = "casigned@example.com",
                        Organization = "Test Organization",
                        Usage = CertificateUsage.All,
                        ValidityDays = 365
                    };
                    _cachedCASignedCert = generator.GenerateTestCertificate(request, ca);
                }
            }
        }
        return _cachedCASignedCert;
    }

    /// <summary>
    /// Generate a fresh certificate (not cached) with custom parameters.
    /// Use this when you need unique certificates for specific test scenarios.
    /// </summary>
    public static X509Certificate2 GenerateFreshCertificate(
        string commonName,
        string email,
        CertificateUsage usage = CertificateUsage.All,
        int validityDays = 365)
    {
        return GenerateCertificate(commonName, email, usage, validityDays);
    }

    /// <summary>
    /// Generate a certificate with specified parameters
    /// </summary>
    private static X509Certificate2 GenerateCertificate(
        string commonName,
        string email,
        CertificateUsage usage,
        int validityDays = 365)
    {
        var generator = new CertificateGenerator();
        var request = new CertificateRequest
        {
            CommonName = commonName,
            Email = email,
            Organization = "PromptResponse Test",
            OrganizationalUnit = "Testing",
            Country = "US",
            ValidityDays = validityDays,
            Usage = usage,
            KeySize = 2048
        };

        return generator.GenerateSelfSignedCertificate(request);
    }

    /// <summary>
    /// Export certificate to PFX format for testing import/export functionality.
    /// </summary>
    public static byte[] ExportCertificateToPfx(X509Certificate2 cert, string password = "TestPassword123!")
    {
        var generator = new CertificateGenerator();
        return generator.ExportPfx(cert, password);
    }

    /// <summary>
    /// Clear all cached certificates (useful for cleanup between test runs).
    /// </summary>
    public static void ClearCache()
    {
        lock (_lock)
        {
            _cachedEmailCert?.Dispose();
            _cachedEmailCert = null;

            _cachedDocumentCert?.Dispose();
            _cachedDocumentCert = null;

            _cachedAllPurposeCert?.Dispose();
            _cachedAllPurposeCert = null;

            _cachedTestCA?.Dispose();
            _cachedTestCA = null;

            _cachedCASignedCert?.Dispose();
            _cachedCASignedCert = null;
        }
    }
}
