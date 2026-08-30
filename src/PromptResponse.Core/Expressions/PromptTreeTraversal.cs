using PromptResponse.Core.Models;

namespace PromptResponse.Core.Expressions;

/// <summary>Enumerates prompts in APR's normative document order.</summary>
internal static class PromptTreeTraversal
{
    internal static IReadOnlyList<Prompt> GetAll(AprDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var prompts = new List<Prompt>();
        foreach (var section in document.Sections) Append(section, prompts);
        return prompts;
    }

    private static void Append(Section section, List<Prompt> prompts)
    {
        prompts.AddRange(section.Prompts);
        foreach (var child in section.Sections) Append(child, prompts);
    }
}
