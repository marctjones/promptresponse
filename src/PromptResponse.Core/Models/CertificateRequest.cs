namespace PromptResponse.Core.Models;

/// <summary>
/// Request for generating a digital certificate for signing documents and emails
/// </summary>
public class CertificateRequest
{
    /// <summary>
    /// Common name (typically the user's full name)
    /// </summary>
    public string CommonName { get; set; } = string.Empty;

    /// <summary>
    /// Email address associated with the certificate
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Organization name (optional)
    /// </summary>
    public string Organization { get; set; } = string.Empty;

    /// <summary>
    /// Organizational unit (optional, e.g., "Engineering", "Legal")
    /// </summary>
    public string OrganizationalUnit { get; set; } = string.Empty;

    /// <summary>
    /// Country code (optional, e.g., "US", "CA")
    /// </summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>
    /// Number of days the certificate should be valid
    /// </summary>
    public int ValidityDays { get; set; } = 365;

    /// <summary>
    /// Intended usage of the certificate (email, document signing, code signing, etc.)
    /// </summary>
    public CertificateUsage Usage { get; set; } = CertificateUsage.EmailSigning;

    /// <summary>
    /// RSA key size in bits (default: 2048, recommended minimum for security)
    /// </summary>
    public int KeySize { get; set; } = 2048;
}
