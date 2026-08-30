using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Cli.Commands.Reporting;
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
            var stats = DocumentStatisticsCollector.Collect(document, filePath);

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

}
