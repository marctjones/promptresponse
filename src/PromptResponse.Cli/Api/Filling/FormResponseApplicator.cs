using PromptResponse.Core.Models;

namespace PromptResponse.Cli.Api.Filling;

/// <summary>
/// Applies caller-provided responses to prompts in a filled-form clone.
/// </summary>
internal sealed class FormResponseApplicator
{
    public ResponseApplicationResult Apply(AprDocument document, IReadOnlyDictionary<string, string> responses)
    {
        var promptsById = new Dictionary<string, Prompt>(StringComparer.Ordinal);
        foreach (var prompt in document.Sections.SelectMany(GetPrompts))
        {
            // Preserve the API's historical depth-first, first-match behavior
            // if an invalid document happens to contain duplicate prompt IDs.
            promptsById.TryAdd(prompt.Id, prompt);
        }
        var missingPromptIds = new List<string>();
        var appliedCount = 0;

        foreach (var (promptId, response) in responses)
        {
            if (!promptsById.TryGetValue(promptId, out var prompt))
            {
                missingPromptIds.Add(promptId);
                continue;
            }

            prompt.Response = response;
            prompt.ResponseMetadata.LastModified = DateTime.UtcNow;
            appliedCount++;
        }

        return new ResponseApplicationResult(appliedCount, missingPromptIds);
    }

    private static IEnumerable<Prompt> GetPrompts(Section section) =>
        section.Prompts.Concat(section.Sections.SelectMany(GetPrompts));
}

internal sealed record ResponseApplicationResult(int AppliedCount, IReadOnlyList<string> MissingPromptIds);
