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
                totalSubsections += section.Subsections.Count;
                totalPrompts += section.Prompts.Count;
                answeredPrompts += section.Prompts.Count(p => !string.IsNullOrWhiteSpace(p.Response));

                foreach (var subsection in section.Subsections)
                {
                    totalPrompts += subsection.Prompts.Count;
                    answeredPrompts += subsection.Prompts.Count(p => !string.IsNullOrWhiteSpace(p.Response));
                }
            }

            if (totalSubsections > 0)
            {
                Console.WriteLine($"  Subsections: {totalSubsections}");
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
                var sectionPrompts = section.Prompts.Count +
                                   section.Subsections.Sum(s => s.Prompts.Count);

                Console.WriteLine($"  {i + 1}. {section.Title}");
                Console.WriteLine($"     ID: {section.Id}");
                Console.WriteLine($"     Prompts: {sectionPrompts}");

                if (section.Subsections.Count > 0)
                {
                    Console.WriteLine($"     Subsections: {section.Subsections.Count}");
                    foreach (var subsection in section.Subsections)
                    {
                        Console.WriteLine($"       - {subsection.Title} ({subsection.Prompts.Count} prompts)");
                    }
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
}
