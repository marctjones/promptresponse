using PromptResponse.Core.Models;

namespace PromptResponse.Core.Services;

/// <summary>
/// Service for verifying digital signatures on APR documents asynchronously.
/// </summary>
public interface ISignatureVerificationService
{
    /// <summary>
    /// Verify the template signature on a document asynchronously.
    /// </summary>
    /// <param name="document">The document to verify</param>
    /// <returns>A result indicating whether the signature is valid</returns>
    Task<SignatureVerificationResult> VerifyTemplateSignatureAsync(AprDocument document);

    /// <summary>
    /// Verify the form signature on a document asynchronously.
    /// </summary>
    /// <param name="document">The document to verify</param>
    /// <returns>A result indicating whether the signature is valid</returns>
    Task<SignatureVerificationResult> VerifyFormSignatureAsync(AprDocument document);
}
