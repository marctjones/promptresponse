using PromptResponse.Core.Models;

namespace PromptResponse.Cli.Api.Filling;

/// <summary>
/// Reads prompt identifiers and completion state from the section tree.
/// </summary>
internal static class FormPromptMetrics
{
    public static List<string> GetPromptIds(AprDocument document) =>
        document.Sections.SelectMany(GetPromptIds).ToList();

    public static double GetCompletionPercentage(AprDocument document)
    {
        var counts = document.Sections
            .SelectMany(GetPrompts)
            .Aggregate((Total: 0, Filled: 0), static (current, prompt) =>
                (current.Total + 1, current.Filled + (!string.IsNullOrWhiteSpace(prompt.Response) ? 1 : 0)));

        return counts.Total == 0 ? 0 : (double)counts.Filled / counts.Total * 100;
    }

    private static IEnumerable<string> GetPromptIds(Section section) =>
        section.Prompts.Select(prompt => prompt.Id).Concat(section.Sections.SelectMany(GetPromptIds));

    private static IEnumerable<Prompt> GetPrompts(Section section) =>
        section.Prompts.Concat(section.Sections.SelectMany(GetPrompts));
}
