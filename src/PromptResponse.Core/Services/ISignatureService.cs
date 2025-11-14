using System.Security.Cryptography.X509Certificates;
using PromptResponse.Core.Models;

namespace PromptResponse.Core.Services;

/// <summary>
/// Service for digitally signing APR documents and verifying signatures.
/// </summary>
/// <remarks>
/// Provides cryptographic signing capabilities for both templates and filled forms.
/// Templates are signed by publishers to establish authenticity.
/// Filled forms are signed by respondents to prove their responses.
/// </remarks>
public interface ISignatureService
{
    /// <summary>
    /// Sign an APR template with the provided certificate.
    /// </summary>
    /// <param name="document">The template document to sign</param>
    /// <param name="certificate">The certificate to sign with (must have private key)</param>
    /// <param name="reason">Optional reason for signing</param>
    /// <returns>A digital signature covering the template structure</returns>
    /// <remarks>
    /// The signature covers:
    /// - Document version
    /// - All sections, subsections, and prompts (structure and labels)
    /// - Template metadata (excluding signatures themselves to avoid circular dependency)
    ///
    /// The signature does NOT cover:
    /// - Response values (those are empty in templates)
    /// - Timestamps (modified, created)
    /// </remarks>
    /// <exception cref="ArgumentException">If document is not a template or certificate lacks private key</exception>
    DigitalSignature SignTemplate(AprDocument document, X509Certificate2 certificate, string? reason = null);

    /// <summary>
    /// Sign a filled APR form with the provided certificate.
    /// </summary>
    /// <param name="document">The filled form document to sign</param>
    /// <param name="certificate">The certificate to sign with (must have private key)</param>
    /// <param name="reason">Optional reason for signing</param>
    /// <returns>A digital signature covering the filled form responses</returns>
    /// <remarks>
    /// The signature covers:
    /// - Document version
    /// - Template reference (ID and version)
    /// - All response values
    /// - Filled form metadata (excluding signatures themselves)
    ///
    /// If the template was signed, the signature also covers the template signatures,
    /// creating a trust chain: "I filled out the official IRS form".
    /// </remarks>
    /// <exception cref="ArgumentException">If document is not a filled form or certificate lacks private key</exception>
    DigitalSignature SignFilledForm(AprDocument document, X509Certificate2 certificate, string? reason = null);

    /// <summary>
    /// Verify a digital signature on a document.
    /// </summary>
    /// <param name="document">The document to verify</param>
    /// <param name="signature">The signature to verify</param>
    /// <returns>A result indicating whether the signature is valid and any issues found</returns>
    /// <remarks>
    /// Verification checks:
    /// 1. Cryptographic validity (signature matches computed hash)
    /// 2. Certificate validity (not expired, proper usage flags)
    /// 3. Document integrity (content hasn't changed since signing)
    ///
    /// Note: Does NOT check certificate trust chain by default.
    /// Callers can optionally verify trust using the certificate thumbprint.
    /// </remarks>
    SignatureVerificationResult VerifySignature(AprDocument document, DigitalSignature signature);

    /// <summary>
    /// Verify all signatures on a document.
    /// </summary>
    /// <param name="document">The document to verify</param>
    /// <returns>A dictionary mapping each signature to its verification result</returns>
    Dictionary<DigitalSignature, SignatureVerificationResult> VerifyAllSignatures(AprDocument document);

    /// <summary>
    /// Check if a document has been signed.
    /// </summary>
    /// <param name="document">The document to check</param>
    /// <returns>True if the document has at least one signature</returns>
    bool IsSigned(AprDocument document);

    /// <summary>
    /// Check if a document has valid signatures.
    /// </summary>
    /// <param name="document">The document to check</param>
    /// <returns>True if all signatures on the document are cryptographically valid</returns>
    bool HasValidSignatures(AprDocument document);
}
