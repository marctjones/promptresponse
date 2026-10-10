using PromptResponse.Core.Models;

namespace PromptResponse.Core.Serialization;

/// <summary>Rejects any document whose version this build does not read.</summary>
internal static class AprCompatibilityGuard
{
    internal static void Validate(AprDocument document)
    {
        if (!AprFormat.IsSupported(document.Version))
            throw new SerializationException($"Unsupported APR version {document.Version ?? "(missing)"}; this build accepts {string.Join(" or ", AprFormat.AcceptedVersions)}");
    }
}
