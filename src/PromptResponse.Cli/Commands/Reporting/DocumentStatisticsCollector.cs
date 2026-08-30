using PromptResponse.Core.Models;

namespace PromptResponse.Cli.Commands.Reporting;

/// <summary>Builds the domain statistics consumed by the stats command's output modes.</summary>
internal static class DocumentStatisticsCollector
{
    internal static DocumentStatistics Collect(AprDocument document, string filePath)
    {
        var stats = new DocumentStatistics { FileName = Path.GetFileName(filePath), FileSize = new FileInfo(filePath).Length, DocumentType = document.DocumentType.ToString(), Title = document.Metadata.Title, SectionCount = document.Sections.Count };
        var prompts = new List<Prompt>();
        foreach (var section in document.Sections)
        {
            var sectionPrompts = new List<Prompt>();
            CollectPrompts(section, sectionPrompts, stats);
            prompts.AddRange(sectionPrompts);
            var answered = sectionPrompts.Count(prompt => !string.IsNullOrWhiteSpace(prompt.Response));
            stats.SectionStatistics.Add(new SectionStatistics { Title = section.Title, PromptCount = sectionPrompts.Count, AnsweredCount = answered, CompletionPercentage = sectionPrompts.Count > 0 ? answered * 100.0 / sectionPrompts.Count : 0 });
        }
        stats.TotalPrompts = prompts.Count;
        stats.AnsweredPrompts = prompts.Count(prompt => !string.IsNullOrWhiteSpace(prompt.Response));
        stats.UnansweredPrompts = stats.TotalPrompts - stats.AnsweredPrompts;
        stats.CompletionPercentage = stats.TotalPrompts > 0 ? stats.AnsweredPrompts * 100.0 / stats.TotalPrompts : 0;
        stats.DataTypeDistribution = prompts.Where(prompt => !string.IsNullOrWhiteSpace(prompt.Hints.ExpectedDataType)).GroupBy(prompt => prompt.Hints.ExpectedDataType).ToDictionary(group => group.Key!, group => group.Count());
        var lengths = prompts.Where(prompt => !string.IsNullOrWhiteSpace(prompt.Response)).Select(prompt => prompt.Response.Length).ToList();
        if (lengths.Any()) { stats.AverageResponseLength = lengths.Average(); stats.MinResponseLength = lengths.Min(); stats.MaxResponseLength = lengths.Max(); }
        stats.PromptsWithSuggestedValues = prompts.Count(prompt => prompt.Hints.SuggestedValues?.Any() == true);
        stats.PromptsWithHelpText = prompts.Count(prompt => !string.IsNullOrWhiteSpace(prompt.Hints.HelpText));
        return stats;
    }

    private static void CollectPrompts(Section section, List<Prompt> prompts, DocumentStatistics stats)
    {
        prompts.AddRange(section.Prompts);
        foreach (var child in section.Sections) { stats.SubsectionCount++; CollectPrompts(child, prompts, stats); }
    }
}

internal sealed class DocumentStatistics
{
    public string FileName { get; set; } = ""; public long FileSize { get; set; } public string DocumentType { get; set; } = ""; public string Title { get; set; } = "";
    public int SectionCount { get; set; } public int SubsectionCount { get; set; } public int TotalPrompts { get; set; } public int AnsweredPrompts { get; set; } public int UnansweredPrompts { get; set; }
    public double CompletionPercentage { get; set; } public double AverageResponseLength { get; set; } public int MinResponseLength { get; set; } public int MaxResponseLength { get; set; }
    public Dictionary<string, int> DataTypeDistribution { get; set; } = new(); public int PromptsWithSuggestedValues { get; set; } public int PromptsWithHelpText { get; set; }
    public List<SectionStatistics> SectionStatistics { get; set; } = new();
}

internal sealed class SectionStatistics { public string Title { get; set; } = ""; public int PromptCount { get; set; } public int AnsweredCount { get; set; } public double CompletionPercentage { get; set; } }
