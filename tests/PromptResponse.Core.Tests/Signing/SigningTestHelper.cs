using System.Security.Cryptography.X509Certificates;
using PromptResponse.Core.Models;
using PromptResponse.Core.Signing;

namespace PromptResponse.Core.Tests.Signing;

internal static class SigningTestHelper
{
    /// <summary>
    /// Puts the submission URL on the document and then signs it.
    /// </summary>
    /// <remarks>
    /// The publisher payload binds <c>metadata.submissionUrl</c> — the field a
    /// submitting client actually reads — so the URL must be on the document before
    /// signing. It used to be passed to the signer and stored a second time on the
    /// signature, and verification recomputed from that private copy: redirecting the
    /// metadata URL then left the signature reporting valid. One fact, one place.
    /// </remarks>
    public static Signature SignTemplateWithUrl(
        AprDocument document, X509Certificate2 certificate, string? url, DateTime signedAtUtc, string id = "publisher")
    {
        document.Metadata.SubmissionUrl = url;
        return AprSigner.SignTemplate(document, certificate, signedAtUtc, id);
    }
}
