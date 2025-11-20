namespace PromptResponse.Core.Models;

/// <summary>
/// Represents the result of verifying a digital signature.
/// </summary>
public class SignatureVerificationResult
{
    /// <summary>
    /// Gets or sets whether the signature is cryptographically valid.
    /// </summary>
    /// <remarks>
    /// True if the signature matches the computed hash of the signed content.
    /// False if the document has been tampered with or signature is corrupted.
    /// </remarks>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets whether the signing certificate is expired.
    /// </summary>
    /// <remarks>
    /// True if the certificate's NotAfter date has passed.
    /// Expired certificates can still have valid signatures if signed before expiration.
    /// </remarks>
    public bool IsCertificateExpired { get; set; }

    /// <summary>
    /// Gets or sets whether the certificate was valid at the time of signing.
    /// </summary>
    /// <remarks>
    /// Checks if SignedAt timestamp falls within certificate's validity period.
    /// </remarks>
    public bool WasValidAtSigningTime { get; set; }

    /// <summary>
    /// Gets or sets error messages encountered during verification.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Gets or sets a single error message for simple error cases.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets warning messages (non-fatal issues).
    /// </summary>
    /// <example>
    /// "Certificate expired", "Certificate is self-signed", "Certificate not in trusted store"
    /// </example>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Gets whether the verification succeeded without errors.
    /// </summary>
    /// <remarks>
    /// True if signature is cryptographically valid and no errors occurred.
    /// Warnings (like expired certificate) don't affect this value.
    /// </remarks>
    public bool Success => IsValid && Errors.Count == 0;

    /// <summary>
    /// Gets a human-readable summary of the verification result.
    /// </summary>
    public string Summary
    {
        get
        {
            if (!IsValid)
                return "Invalid signature - document may have been tampered with";

            if (IsCertificateExpired && !WasValidAtSigningTime)
                return "Certificate was expired at time of signing";

            if (IsCertificateExpired)
                return "Valid signature (certificate has since expired)";

            if (Errors.Count > 0)
                return $"Signature verification failed: {string.Join(", ", Errors)}";

            if (Warnings.Count > 0)
                return $"Valid signature with warnings: {string.Join(", ", Warnings)}";

            return "Valid signature";
        }
    }
}
