using PromptResponse.Core.Models;

namespace PromptResponse.Core.Serialization;

/// <summary>Rejects anything outside the sole supported APR wire format.</summary>
internal static class AprCompatibilityGuard
{
    internal static void Validate(AprDocument document)
    {
        if (!string.Equals(document.Version, AprFormat.CurrentVersion, StringComparison.Ordinal))
            throw new SerializationException($"Unsupported APR version {document.Version ?? "(missing)"}; this build accepts only {AprFormat.CurrentVersion}");

        if (document.Metadata?.Extensions?.ContainsKey("submissionUrl") == true)
            throw new SerializationException("metadata.submissionUrl is retired; use metadata.submissionUrls as an array of strings");

    }
}
