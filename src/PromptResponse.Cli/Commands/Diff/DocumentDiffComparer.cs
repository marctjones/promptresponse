using PromptResponse.Core.Models;

namespace PromptResponse.Cli.Commands.Diff;

/// <summary>Compares the APR fields that the CLI diff command reports.</summary>
internal static class DocumentDiffComparer
{
    internal static IReadOnlyList<Difference> Compare(AprDocument first, AprDocument second)
    {
        var differences = new List<Difference>();
        CompareDocumentMetadata(first, second, differences);
        CompareSections(first.Sections, second.Sections, null, differences);
        return differences;
    }

    private static void CompareDocumentMetadata(AprDocument first, AprDocument second, List<Difference> differences)
    {
        AddWhenDifferent(differences, "Metadata", "Title", first.Metadata.Title, second.Metadata.Title);
        AddWhenDifferent(differences, "Metadata", "Document Type", first.DocumentType.ToString(), second.DocumentType.ToString());
        AddWhenDifferent(differences, "Structure", "Section Count", first.Sections.Count.ToString(), second.Sections.Count.ToString());
    }

    private static void CompareSections(List<Section> first, List<Section> second, string? parentPath, List<Difference> differences)
    {
        for (var index = 0; index < Math.Max(first.Count, second.Count); index++)
        {
            if (index >= first.Count || index >= second.Count)
            {
                differences.Add(new Difference("Structure", SectionPath(parentPath, index), index < first.Count ? first[index].Title : null, index < second.Count ? second[index].Title : null));
                continue;
            }

            var firstSection = first[index];
            var secondSection = second[index];
            var path = SectionPath(parentPath, index);
            AddWhenDifferent(differences, "Structure", $"{path}.Title", firstSection.Title, secondSection.Title);

            var contentsPath = parentPath is null
                ? $"Section '{firstSection.Title}'"
                : $"{parentPath} / '{firstSection.Title}'";
            ComparePrompts(firstSection.Prompts, secondSection.Prompts, contentsPath, differences);
            CompareSections(firstSection.Sections, secondSection.Sections, contentsPath, differences);
        }
    }

    private static void ComparePrompts(List<Prompt> first, List<Prompt> second, string path, List<Difference> differences)
    {
        var firstById = first.ToDictionary(prompt => prompt.Id);
        var secondById = second.ToDictionary(prompt => prompt.Id);

        foreach (var id in firstById.Keys.Union(secondById.Keys))
        {
            var hasFirst = firstById.TryGetValue(id, out var firstPrompt);
            var hasSecond = secondById.TryGetValue(id, out var secondPrompt);
            if (!hasFirst || !hasSecond)
            {
                differences.Add(new Difference(
                    "Prompt Missing",
                    $"{path} / Prompt '{id}'",
                    hasFirst ? Describe(firstPrompt!) : null,
                    hasSecond ? Describe(secondPrompt!) : null));
                continue;
            }

            AddWhenDifferent(differences, "Response", $"{path} / '{firstPrompt!.Label}'", firstPrompt.Response, secondPrompt!.Response);
            AddWhenDifferent(differences, "Label", $"{path} / Prompt '{id}'", firstPrompt.Label, secondPrompt.Label);
        }
    }

    private static string SectionPath(string? parentPath, int index) => parentPath is null ? $"Section[{index}]" : $"{parentPath} / Section[{index}]";

    private static string Describe(Prompt prompt) => $"{prompt.Label}: {prompt.Response}";

    private static void AddWhenDifferent(List<Difference> differences, string type, string path, string? first, string? second)
    {
        if (first != second)
        {
            differences.Add(new Difference(type, path, first, second));
        }
    }
}
