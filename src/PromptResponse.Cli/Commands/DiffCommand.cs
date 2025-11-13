using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Cli.Commands;

/// <summary>
/// Compares two APR files and shows differences.
/// </summary>
public class DiffCommand : ICommand
{
    private readonly IAprSerializer _serializer;

    public DiffCommand(IAprSerializer serializer)
    {
        _serializer = serializer;
    }

    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Error: Two file paths required");
            Console.Error.WriteLine("Usage: apr diff <file1> <file2>");
            return 1;
        }

        var file1Path = args[0];
        var file2Path = args[1];

        if (!File.Exists(file1Path))
        {
            Console.Error.WriteLine($"Error: File not found: {file1Path}");
            return 1;
        }

        if (!File.Exists(file2Path))
        {
            Console.Error.WriteLine($"Error: File not found: {file2Path}");
            return 1;
        }

        try
        {
            // Load both documents
            var json1 = await File.ReadAllTextAsync(file1Path);
            var doc1 = _serializer.Deserialize(json1);

            var json2 = await File.ReadAllTextAsync(file2Path);
            var doc2 = _serializer.Deserialize(json2);

            // Compare
            var differences = CompareDocuments(doc1, doc2);

            // Display results
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine("Document Comparison");
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine($"File 1: {Path.GetFileName(file1Path)}");
            Console.WriteLine($"File 2: {Path.GetFileName(file2Path)}");
            Console.WriteLine();

            if (differences.Count == 0)
            {
                Console.WriteLine("✓ Documents are identical (responses match)");
                Console.WriteLine();
                Console.WriteLine("═══════════════════════════════════════");
                return 0;
            }

            Console.WriteLine($"Found {differences.Count} difference(s):");
            Console.WriteLine();

            foreach (var diff in differences)
            {
                Console.WriteLine($"[{diff.Type}] {diff.Path}");
                Console.WriteLine($"  File 1: {diff.Value1 ?? "(empty)"}");
                Console.WriteLine($"  File 2: {diff.Value2 ?? "(empty)"}");
                Console.WriteLine();
            }

            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine($"Summary: {differences.Count} difference(s) found");
            Console.WriteLine("═══════════════════════════════════════");

            return differences.Count > 0 ? 1 : 0;
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

    private List<Difference> CompareDocuments(AprDocument doc1, AprDocument doc2)
    {
        var differences = new List<Difference>();

        // Compare metadata
        if (doc1.Metadata.Title != doc2.Metadata.Title)
        {
            differences.Add(new Difference
            {
                Type = "Metadata",
                Path = "Title",
                Value1 = doc1.Metadata.Title,
                Value2 = doc2.Metadata.Title
            });
        }

        if (doc1.DocumentType != doc2.DocumentType)
        {
            differences.Add(new Difference
            {
                Type = "Metadata",
                Path = "Document Type",
                Value1 = doc1.DocumentType.ToString(),
                Value2 = doc2.DocumentType.ToString()
            });
        }

        // Compare structure - section count
        if (doc1.Sections.Count != doc2.Sections.Count)
        {
            differences.Add(new Difference
            {
                Type = "Structure",
                Path = "Section Count",
                Value1 = doc1.Sections.Count.ToString(),
                Value2 = doc2.Sections.Count.ToString()
            });
        }

        // Compare sections
        var maxSections = Math.Max(doc1.Sections.Count, doc2.Sections.Count);
        for (int i = 0; i < maxSections; i++)
        {
            if (i >= doc1.Sections.Count)
            {
                differences.Add(new Difference
                {
                    Type = "Structure",
                    Path = $"Section[{i}]",
                    Value1 = null,
                    Value2 = doc2.Sections[i].Title
                });
                continue;
            }

            if (i >= doc2.Sections.Count)
            {
                differences.Add(new Difference
                {
                    Type = "Structure",
                    Path = $"Section[{i}]",
                    Value1 = doc1.Sections[i].Title,
                    Value2 = null
                });
                continue;
            }

            var section1 = doc1.Sections[i];
            var section2 = doc2.Sections[i];

            // Compare section titles
            if (section1.Title != section2.Title)
            {
                differences.Add(new Difference
                {
                    Type = "Structure",
                    Path = $"Section[{i}].Title",
                    Value1 = section1.Title,
                    Value2 = section2.Title
                });
            }

            // Compare prompts in section
            ComparePrompts(section1.Prompts, section2.Prompts, $"Section '{section1.Title}'", differences);

            // Compare subsections
            var maxSubsections = Math.Max(section1.Subsections.Count, section2.Subsections.Count);
            for (int j = 0; j < maxSubsections; j++)
            {
                if (j >= section1.Subsections.Count || j >= section2.Subsections.Count)
                {
                    differences.Add(new Difference
                    {
                        Type = "Structure",
                        Path = $"Section '{section1.Title}' / Subsection[{j}]",
                        Value1 = j < section1.Subsections.Count ? section1.Subsections[j].Title : null,
                        Value2 = j < section2.Subsections.Count ? section2.Subsections[j].Title : null
                    });
                    continue;
                }

                var subsection1 = section1.Subsections[j];
                var subsection2 = section2.Subsections[j];

                ComparePrompts(subsection1.Prompts, subsection2.Prompts,
                    $"Section '{section1.Title}' / Subsection '{subsection1.Title}'", differences);
            }
        }

        return differences;
    }

    private void ComparePrompts(List<Prompt> prompts1, List<Prompt> prompts2, string path, List<Difference> differences)
    {
        // Create dictionaries for easier comparison by ID
        var dict1 = prompts1.ToDictionary(p => p.Id, p => p);
        var dict2 = prompts2.ToDictionary(p => p.Id, p => p);

        var allIds = dict1.Keys.Union(dict2.Keys).ToList();

        foreach (var id in allIds)
        {
            var exists1 = dict1.TryGetValue(id, out var prompt1);
            var exists2 = dict2.TryGetValue(id, out var prompt2);

            if (!exists1)
            {
                differences.Add(new Difference
                {
                    Type = "Prompt Missing",
                    Path = $"{path} / Prompt '{id}'",
                    Value1 = null,
                    Value2 = $"{prompt2!.Label}: {prompt2.Response}"
                });
                continue;
            }

            if (!exists2)
            {
                differences.Add(new Difference
                {
                    Type = "Prompt Missing",
                    Path = $"{path} / Prompt '{id}'",
                    Value1 = $"{prompt1!.Label}: {prompt1.Response}",
                    Value2 = null
                });
                continue;
            }

            // Both exist, compare responses
            if (prompt1!.Response != prompt2!.Response)
            {
                differences.Add(new Difference
                {
                    Type = "Response",
                    Path = $"{path} / '{prompt1.Label}'",
                    Value1 = prompt1.Response,
                    Value2 = prompt2.Response
                });
            }

            // Also compare labels to detect structure changes
            if (prompt1.Label != prompt2.Label)
            {
                differences.Add(new Difference
                {
                    Type = "Label",
                    Path = $"{path} / Prompt '{id}'",
                    Value1 = prompt1.Label,
                    Value2 = prompt2.Label
                });
            }
        }
    }

    private class Difference
    {
        public string Type { get; set; } = "";
        public string Path { get; set; } = "";
        public string? Value1 { get; set; }
        public string? Value2 { get; set; }
    }
}
