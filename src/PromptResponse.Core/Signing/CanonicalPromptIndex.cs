using PromptResponse.Core.Models;

namespace PromptResponse.Core.Signing;

/// <summary>Builds the ordinal prompt lookup used by scoped filler payloads.</summary>
internal static class CanonicalPromptIndex
{
    internal static IReadOnlyDictionary<string, Prompt> Build(AprDocument document)
    {
        var prompts = new Dictionary<string, Prompt>(StringComparer.Ordinal);
        foreach (var section in document.Sections) Collect(section, prompts);
        return prompts;
    }

    private static void Collect(Section section, Dictionary<string, Prompt> prompts)
    {
        foreach (var prompt in section.Prompts) if (!string.IsNullOrEmpty(prompt.Id)) prompts[prompt.Id] = prompt;
        foreach (var child in section.Sections) Collect(child, prompts);
    }
}
