using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using System.Text.Json;

namespace PromptResponse.Cli.Commands;

/// <summary>
/// Shows detailed statistics about an APR file.
/// </summary>
public class StatsCommand : ICommand
{
    private readonly IAprSerializer _serializer;

    public StatsCommand(IAprSerializer serializer)
    {
        _serializer = serializer;
    }

    public async Task<int> ExecuteAsync(string[] args)
    {
        var jsonOutput = args.Contains("--json") || args.Contains("-j");
        var filePath = args.FirstOrDefault(a => !a.StartsWith("-"));

        if (string.IsNullOrEmpty(filePath))
        {
            Console.Error.WriteLine("Error: File path required");
            Console.Error.WriteLine("Usage: apr stats <file> [--json]");
            return 1;
        }

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Error: File not found: {filePath}");
            return 1;
        }

        try
        {
            // Load document
            var json = await File.ReadAllTextAsync(filePath);
            var document = _serializer.Deserialize(json);

            // Gather statistics
            var stats = GatherStatistics(document, filePath);

            // Output
            if (jsonOutput)
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                Console.WriteLine(JsonSerializer.Serialize(stats, jsonOptions));
            }
            else
            {
                DisplayStatistics(stats);
                SignatureNotice.Write(document);
            }

            return 0;
        }
        catch (SerializationException ex)
        {
            Console.Error.WriteLine($"Error: Failed to load APR file: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private DocumentStatistics GatherStatistics(AprDocument document, string filePath)
    {
        var stats = new DocumentStatistics
        {
            FileName = Path.GetFileName(filePath),
            FileSize = new FileInfo(filePath).Length,
            DocumentType = document.DocumentType.ToString(),
            Title = document.Metadata.Title,
            SectionCount = document.Sections.Count
        };

        var allPrompts = new List<Prompt>();
        var sectionStats = new List<SectionStatistics>();

        foreach (var section in document.Sections)
        {
            var sectionPrompts = new List<Prompt>();
            CollectPromptsFromSection(section, sectionPrompts, stats);

            allPrompts.AddRange(sectionPrompts);

            var answered = sectionPrompts.Count(p => !string.IsNullOrWhiteSpace(p.Response));
            sectionStats.Add(new SectionStatistics
            {
                Title = section.Title,
                PromptCount = sectionPrompts.Count,
                AnsweredCount = answered,
                CompletionPercentage = sectionPrompts.Count > 0 ? (answered * 100.0 / sectionPrompts.Count) : 0
            });
        }

        stats.TotalPrompts = allPrompts.Count;
        stats.AnsweredPrompts = allPrompts.Count(p => !string.IsNullOrWhiteSpace(p.Response));
        stats.UnansweredPrompts = stats.TotalPrompts - stats.AnsweredPrompts;
        stats.CompletionPercentage = stats.TotalPrompts > 0
            ? (stats.AnsweredPrompts * 100.0 / stats.TotalPrompts)
            : 0;

        // Data type distribution
        var dataTypes = allPrompts
            .Where(p => !string.IsNullOrWhiteSpace(p.Hints.ExpectedDataType))
            .GroupBy(p => p.Hints.ExpectedDataType)
            .ToDictionary(g => g.Key!, g => g.Count());

        stats.DataTypeDistribution = dataTypes;

        // Response length statistics (for answered prompts only)
        var answeredResponses = allPrompts
            .Where(p => !string.IsNullOrWhiteSpace(p.Response))
            .Select(p => p.Response.Length)
            .ToList();

        if (answeredResponses.Any())
        {
            stats.AverageResponseLength = answeredResponses.Average();
            stats.MinResponseLength = answeredResponses.Min();
            stats.MaxResponseLength = answeredResponses.Max();
        }

        // Prompts with suggested values
        stats.PromptsWithSuggestedValues = allPrompts.Count(p =>
            p.Hints.SuggestedValues != null && p.Hints.SuggestedValues.Any());

        // Prompts with help text
        stats.PromptsWithHelpText = allPrompts.Count(p =>
            !string.IsNullOrWhiteSpace(p.Hints.HelpText));

        stats.SectionStatistics = sectionStats;

        return stats;
    }

    private void CollectPromptsFromSection(Section section, List<Prompt> prompts, DocumentStatistics stats)
    {
        prompts.AddRange(section.Prompts);

        foreach (var childSection in section.Sections)
        {
            stats.SubsectionCount++;
            CollectPromptsFromSection(childSection, prompts, stats);
        }
    }

    private void DisplayStatistics(DocumentStatistics stats)
    {
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine($"Statistics: {stats.FileName}");
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine();

        Console.WriteLine("Document:");
        Console.WriteLine($"  Title: {stats.Title}");
        Console.WriteLine($"  Type: {stats.DocumentType}");
        Console.WriteLine($"  File Size: {FormatBytes(stats.FileSize)}");
        Console.WriteLine();

        Console.WriteLine("Structure:");
        Console.WriteLine($"  Sections: {stats.SectionCount}");
        if (stats.SubsectionCount > 0)
        {
            Console.WriteLine($"  Subsections: {stats.SubsectionCount}");
        }
        Console.WriteLine($"  Total Prompts: {stats.TotalPrompts}");
        Console.WriteLine();

        Console.WriteLine("Completion:");
        Console.WriteLine($"  Answered: {stats.AnsweredPrompts}");
        Console.WriteLine($"  Unanswered: {stats.UnansweredPrompts}");
        Console.WriteLine($"  Completion: {stats.CompletionPercentage:F1}%");
        Console.WriteLine();

        if (stats.AnsweredPrompts > 0)
        {
            Console.WriteLine("Response Lengths:");
            Console.WriteLine($"  Average: {stats.AverageResponseLength:F1} characters");
            Console.WriteLine($"  Min: {stats.MinResponseLength} characters");
            Console.WriteLine($"  Max: {stats.MaxResponseLength} characters");
            Console.WriteLine();
        }

        if (stats.DataTypeDistribution.Any())
        {
            Console.WriteLine("Data Type Distribution:");
            foreach (var dt in stats.DataTypeDistribution.OrderByDescending(kv => kv.Value))
            {
                Console.WriteLine($"  {dt.Key}: {dt.Value} ({dt.Value * 100.0 / stats.TotalPrompts:F1}%)");
            }
            Console.WriteLine();
        }

        Console.WriteLine("Field Assistance:");
        Console.WriteLine($"  Prompts with suggested values: {stats.PromptsWithSuggestedValues}");
        Console.WriteLine($"  Prompts with help text: {stats.PromptsWithHelpText}");
        Console.WriteLine();

        Console.WriteLine("Completion by Section:");
        foreach (var section in stats.SectionStatistics)
        {
            var bar = CreateProgressBar(section.CompletionPercentage, 20);
            Console.WriteLine($"  {section.Title}");
            Console.WriteLine($"    {section.AnsweredCount}/{section.PromptCount} [{bar}] {section.CompletionPercentage:F1}%");
        }

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════");
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private string CreateProgressBar(double percentage, int width)
    {
        var filled = (int)(percentage / 100.0 * width);
        var empty = width - filled;
        return new string('█', filled) + new string('░', empty);
    }

    private class DocumentStatistics
    {
        public string FileName { get; set; } = "";
        public long FileSize { get; set; }
        public string DocumentType { get; set; } = "";
        public string Title { get; set; } = "";
        public int SectionCount { get; set; }
        public int SubsectionCount { get; set; }
        public int TotalPrompts { get; set; }
        public int AnsweredPrompts { get; set; }
        public int UnansweredPrompts { get; set; }
        public double CompletionPercentage { get; set; }
        public double AverageResponseLength { get; set; }
        public int MinResponseLength { get; set; }
        public int MaxResponseLength { get; set; }
        public Dictionary<string, int> DataTypeDistribution { get; set; } = new();
        public int PromptsWithSuggestedValues { get; set; }
        public int PromptsWithHelpText { get; set; }
        public List<SectionStatistics> SectionStatistics { get; set; } = new();
    }

    private class SectionStatistics
    {
        public string Title { get; set; } = "";
        public int PromptCount { get; set; }
        public int AnsweredCount { get; set; }
        public double CompletionPercentage { get; set; }
    }
}
