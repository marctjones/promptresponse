using System.Security.Cryptography;
using PromptResponse.Core.Models;

namespace PromptResponse.Core.Signing;

/// <summary>The result of verifying one <see cref="Signature"/>.</summary>
/// <param name="Id">The signature's id.</param>
/// <param name="Role">Publisher or filler.</param>
/// <param name="SignerName">The signer's claimed name.</param>
/// <param name="IsValid">Whether the signature verifies against the current content.</param>
/// <param name="Status">A human-readable explanation.</param>
public sealed record SignatureVerification(string Id, SignatureRole Role, string SignerName, bool IsValid, string Status);

/// <summary>
/// Verifies detached APR signatures by recomputing the canonical payload over the
/// document's <em>current</em> content and checking it against each signature's
/// embedded public key. A valid result proves the covered content is unchanged
/// since signing and that the holder of the matching private key signed it.
/// </summary>
/// <remarks>
/// Trust is key-based: verification proves "the holder of this public key signed
/// this content", not "this named identity". Binding a key to a real-world
/// identity (e.g. matching a publisher's known/trusted key, or an X.509
/// certificate chain) is an additive layer on top.
/// </remarks>
public static class AprVerifier
{
    /// <summary>Verifies every signature on the document.</summary>
    public static IReadOnlyList<SignatureVerification> VerifyAll(AprDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Signatures is null || document.Signatures.Count == 0)
        {
            return Array.Empty<SignatureVerification>();
        }
        return document.Signatures.Select(s => Verify(document, s)).ToList();
    }

    /// <summary>Verifies a single signature against the document's current content.</summary>
    public static SignatureVerification Verify(AprDocument document, Signature signature)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(signature);

        try
        {
            var payload = signature.Scope == "template"
                ? AprCanonicalizer.PublisherPayload(document, signature.Signer, signature.SubmissionUrl, signature.SignedAt)
                : AprCanonicalizer.FillerPayload(document, signature.Fields, signature.Signer, signature.SignedAt);

            using var ec = ECDsa.Create();
            ec.ImportFromPem(signature.Signer.PublicKey);
            var ok = ec.VerifyData(payload, Convert.FromBase64String(signature.Value), HashAlgorithmName.SHA256);

            return new SignatureVerification(
                signature.Id, signature.Role, signature.Signer.Name, ok,
                ok ? "valid" : "invalid — the covered content was altered, or the signature does not match the key");
        }
        catch (Exception ex)
        {
            return new SignatureVerification(signature.Id, signature.Role, signature.Signer.Name, false, "error: " + ex.Message);
        }
    }
}
