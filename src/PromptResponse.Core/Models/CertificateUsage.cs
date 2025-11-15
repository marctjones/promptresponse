namespace PromptResponse.Core.Models;

/// <summary>
/// Defines the intended usage for a digital certificate.
/// These map to standard X.509 Extended Key Usage OIDs used by commercial CAs.
/// </summary>
[Flags]
public enum CertificateUsage
{
    /// <summary>
    /// Email signing and encryption (S/MIME)
    /// OID: 1.3.6.1.5.5.7.3.4 (id-kp-emailProtection)
    /// Compatible with: Outlook, Gmail, Thunderbird
    /// </summary>
    EmailSigning = 1,

    /// <summary>
    /// Document signing (PDF, Word, etc.)
    /// OID: 1.3.6.1.4.1.311.10.3.12 (Microsoft Document Signing)
    /// Compatible with: Adobe Acrobat, Microsoft Word, DocuSign
    /// </summary>
    DocumentSigning = 2,

    /// <summary>
    /// Code signing (executables, scripts, etc.)
    /// OID: 1.3.6.1.5.5.7.3.3 (id-kp-codeSigning)
    /// Compatible with: Windows Authenticode, macOS codesign
    /// </summary>
    CodeSigning = 4,

    /// <summary>
    /// All supported usage types
    /// </summary>
    All = EmailSigning | DocumentSigning | CodeSigning
}
