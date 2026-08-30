using PromptResponse.Core.Models;

namespace PromptResponse.Core.Serialization;

/// <summary>Rejects retired wire members that cannot be interpreted safely.</summary>
internal static class AprCompatibilityGuard
{
    internal static void Validate(AprDocument document)
    {
        if (document.Metadata?.Extensions?.ContainsKey("submissionUrl") == true)
            throw new SerializationException("metadata.submissionUrl is retired; use metadata.submissionUrls as an array of strings");
    }
}
