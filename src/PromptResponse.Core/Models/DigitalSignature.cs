namespace PromptResponse.Core.Models;

/// <summary>
/// Represents a digital signature applied to an APR document.
/// </summary>
/// <remarks>
/// Digital signatures provide cryptographic proof of:
/// - Who signed the document (authentication)
/// - The document hasn't been modified since signing (integrity)
/// - The signer cannot deny having signed it (non-repudiation)
/// - When the document was signed (timestamp)
///
/// Templates can be signed by publishers (e.g., IRS, HR department) to establish authenticity.
/// Filled forms can be signed by the person who completed them to prove their responses.
/// </remarks>
public class DigitalSignature
{
    /// <summary>
    /// Gets or sets the common name of the signer (from certificate CN field).
    /// </summary>
    /// <example>
    /// "John Doe", "Internal Revenue Service", "Acme Corp HR Department"
    /// </example>
    public string SignerName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email address of the signer (from certificate email field).
    /// </summary>
    /// <example>
    /// "john.doe@example.com", "forms@irs.gov"
    /// </example>
    public string? SignerEmail { get; set; }

    /// <summary>
    /// Gets or sets the organization of the signer (from certificate O field).
    /// </summary>
    /// <example>
    /// "Acme Corporation", "U.S. Department of Treasury"
    /// </example>
    public string? SignerOrganization { get; set; }

    /// <summary>
    /// Gets or sets the organizational unit of the signer (from certificate OU field).
    /// </summary>
    /// <example>
    /// "Human Resources", "Tax Forms Division"
    /// </example>
    public string? SignerOrganizationalUnit { get; set; }

    /// <summary>
    /// Gets or sets the certificate issuer name (Certificate Authority).
    /// </summary>
    /// <remarks>
    /// For self-signed certificates, this will match the signer name.
    /// For commercial certificates, this identifies the CA (e.g., "DigiCert", "Let's Encrypt").
    /// </remarks>
    public string CertificateIssuer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the certificate thumbprint (SHA-256 hash of the certificate).
    /// </summary>
    /// <remarks>
    /// Used to uniquely identify the certificate that created this signature.
    /// Can be used to look up the certificate in a certificate store.
    /// </remarks>
    public string CertificateThumbprint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the signature was created.
    /// </summary>
    /// <remarks>
    /// Should be UTC timestamp. Proves when the document was signed.
    /// </remarks>
    public DateTime SignedAt { get; set; }

    /// <summary>
    /// Gets or sets the cryptographic signature data (Base64-encoded).
    /// </summary>
    /// <remarks>
    /// This is the actual digital signature created by signing the document hash
    /// with the signer's private key. It can be verified using the public key
    /// from the certificate identified by CertificateThumbprint.
    ///
    /// Format: Base64-encoded RSA signature of SHA-256 hash of the signed content.
    /// </remarks>
    public string SignatureData { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets what was signed (template structure or filled form responses).
    /// </summary>
    /// <remarks>
    /// - "template": Signature covers the template structure (sections, subsections, prompts)
    /// - "filledForm": Signature covers the filled form responses and template reference
    /// </remarks>
    public string SignatureType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hash algorithm used for signing.
    /// </summary>
    /// <example>
    /// "SHA256", "SHA512"
    /// </example>
    public string HashAlgorithm { get; set; } = "SHA256";

    /// <summary>
    /// Gets or sets a comment or reason for signing.
    /// </summary>
    /// <remarks>
    /// Optional field to explain why the document was signed.
    /// </remarks>
    /// <example>
    /// "Official IRS form for tax year 2025", "Reviewed and approved", "Attestation of accuracy"
    /// </example>
    public string? SignatureReason { get; set; }
}
