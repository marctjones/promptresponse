using PromptResponse.Core.Serialization;

namespace PromptResponse.Cli.Commands;

/// <summary>
/// Shows information about an APR file.
/// </summary>
public class InfoCommand : ICommand
{
    private readonly IAprSerializer _serializer;

    public InfoCommand(IAprSerializer serializer)
    {
        _serializer = serializer;
    }

    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: File path required");
            Console.Error.WriteLine("Usage: apr info <file>");
            return 1;
        }

        var filePath = args[0];

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

            // Display information
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine($"File: {Path.GetFileName(filePath)}");
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine();

            // Metadata
            Console.WriteLine("Document Information:");
            Console.WriteLine($"  Version: {document.Version}");
            Console.WriteLine($"  Type: {document.DocumentType}");
            Console.WriteLine($"  Title: {document.Metadata.Title}");

            if (!string.IsNullOrWhiteSpace(document.Metadata.Description))
            {
                Console.WriteLine($"  Description: {document.Metadata.Description}");
            }

            if (document.DocumentType == Core.Models.DocumentType.Template)
            {
                if (!string.IsNullOrWhiteSpace(document.Metadata.Author))
                {
                    Console.WriteLine($"  Author: {document.Metadata.Author}");
                }
                if (!string.IsNullOrWhiteSpace(document.Metadata.TemplateId))
                {
                    Console.WriteLine($"  Template ID: {document.Metadata.TemplateId}");
                }
                if (!string.IsNullOrWhiteSpace(document.Metadata.TemplateVersion))
                {
                    Console.WriteLine($"  Template Version: {document.Metadata.TemplateVersion}");
                }
                if (document.Metadata.Created.HasValue)
                {
                    Console.WriteLine($"  Created: {document.Metadata.Created:yyyy-MM-dd HH:mm:ss} UTC");
                }
            }
            else // FilledForm
            {
                if (!string.IsNullOrWhiteSpace(document.Metadata.TemplateId))
                {
                    Console.WriteLine($"  Based on Template: {document.Metadata.TemplateId}");
                }
                if (!string.IsNullOrWhiteSpace(document.Metadata.TemplateVersion))
                {
                    Console.WriteLine($"  Template Version: {document.Metadata.TemplateVersion}");
                }
                if (!string.IsNullOrWhiteSpace(document.Metadata.FilledBy))
                {
                    Console.WriteLine($"  Filled By: {document.Metadata.FilledBy}");
                }
                if (document.Metadata.FilledDate.HasValue)
                {
                    Console.WriteLine($"  Filled Date: {document.Metadata.FilledDate:yyyy-MM-dd HH:mm:ss} UTC");
                }
            }

            if (document.Metadata.Modified.HasValue)
            {
                Console.WriteLine($"  Last Modified: {document.Metadata.Modified:yyyy-MM-dd HH:mm:ss} UTC");
            }

            Console.WriteLine();

            // Structure
            Console.WriteLine("Structure:");
            Console.WriteLine($"  Sections: {document.Sections.Count}");

            var totalPrompts = 0;
            var totalSubsections = 0;
            var answeredPrompts = 0;

            foreach (var section in document.Sections)
            {
                CountSectionStats(section, ref totalPrompts, ref totalSubsections, ref answeredPrompts);
            }

            if (totalSubsections > 0)
            {
                Console.WriteLine($"  Child Sections: {totalSubsections}");
            }
            Console.WriteLine($"  Total Prompts: {totalPrompts}");

            if (document.DocumentType == Core.Models.DocumentType.FilledForm)
            {
                var percentComplete = totalPrompts > 0 ? (answeredPrompts * 100.0 / totalPrompts) : 0;
                Console.WriteLine($"  Answered: {answeredPrompts} ({percentComplete:F1}%)");
            }

            Console.WriteLine();

            // Section details
            Console.WriteLine("Sections:");
            for (int i = 0; i < document.Sections.Count; i++)
            {
                var section = document.Sections[i];
                var sectionPrompts = CountPromptsInSection(section);

                Console.WriteLine($"  {i + 1}. {section.Title}");
                Console.WriteLine($"     ID: {section.Id}");
                Console.WriteLine($"     Prompts: {sectionPrompts}");

                if (section.Sections.Count > 0)
                {
                    Console.WriteLine($"     Child Sections: {section.Sections.Count}");
                    DisplayChildSections(section.Sections, "       ");
                }
            }

            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════");

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

    private void CountSectionStats(Core.Models.Section section, ref int totalPrompts, ref int totalSubsections, ref int answeredPrompts)
    {
        totalPrompts += section.Prompts.Count;
        answeredPrompts += section.Prompts.Count(p => !string.IsNullOrWhiteSpace(p.Response));

        foreach (var childSection in section.Sections)
        {
            totalSubsections++;
            CountSectionStats(childSection, ref totalPrompts, ref totalSubsections, ref answeredPrompts);
        }
    }

    private int CountPromptsInSection(Core.Models.Section section)
    {
        var count = section.Prompts.Count;
        foreach (var childSection in section.Sections)
        {
            count += CountPromptsInSection(childSection);
        }
        return count;
    }

    private void DisplayChildSections(List<Core.Models.Section> sections, string indent)
    {
        foreach (var section in sections)
        {
            var promptCount = CountPromptsInSection(section);
            Console.WriteLine($"{indent}- {section.Title} ({promptCount} prompts)");
            if (section.Sections.Count > 0)
            {
                DisplayChildSections(section.Sections, indent + "  ");
            }
        }
    }
}
