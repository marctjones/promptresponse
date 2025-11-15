using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Cli.Commands;

/// <summary>
/// Creates a new APR template file.
/// </summary>
public class NewCommand : ICommand
{
    private readonly IAprSerializer _serializer;

    public NewCommand(IAprSerializer serializer)
    {
        _serializer = serializer;
    }

    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: File path required");
            Console.Error.WriteLine("Usage: apr new <file>");
            return 1;
        }

        var filePath = args[0];

        // Ensure APR extension (.aprt for templates by default)
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension != ".apr" && extension != ".aprt" && extension != ".aprf")
        {
            // Default to .aprt for templates
            filePath += ".aprt";
        }

        if (File.Exists(filePath))
        {
            Console.Error.Write($"File already exists: {filePath}. Overwrite? (y/N): ");
            var response = Console.ReadLine();
            if (response?.Trim().ToLowerInvariant() != "y")
            {
                Console.WriteLine("Cancelled.");
                return 0;
            }
        }

        try
        {
            // Gather template information
            Console.WriteLine("Creating new APR template...");
            Console.WriteLine();

            Console.Write("Template title: ");
            var title = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                Console.Error.WriteLine("Error: Title is required");
                return 1;
            }

            Console.Write("Description (optional): ");
            var description = Console.ReadLine()?.Trim();

            Console.Write("Author (optional): ");
            var author = Console.ReadLine()?.Trim();

            Console.Write("Template ID (optional): ");
            var templateId = Console.ReadLine()?.Trim();

            // Create minimal template
            var document = new AprDocument
            {
                Version = "1.0",
                DocumentType = DocumentType.Template,
                Metadata = new Metadata
                {
                    Title = title,
                    Description = string.IsNullOrWhiteSpace(description) ? null : description,
                    Author = string.IsNullOrWhiteSpace(author) ? null : author,
                    TemplateId = string.IsNullOrWhiteSpace(templateId) ? null : templateId,
                    TemplateVersion = "1.0",
                    Created = DateTime.UtcNow,
                    Modified = DateTime.UtcNow
                },
                Sections = new List<Section>
                {
                    new()
                    {
                        Id = "section_001",
                        Title = "Section 1",
                        Description = "First section - edit this",
                        Prompts = new List<Prompt>
                        {
                            new()
                            {
                                Id = "prompt_001",
                                Label = "Example Question",
                                Response = "",
                                Hints = new PromptHints
                                {
                                    Placeholder = "Enter your answer here",
                                    ExpectedDataType = "text",
                                    HelpText = "This is an example prompt - edit or remove it"
                                }
                            }
                        }
                    }
                }
            };

            // Serialize and save
            var json = _serializer.Serialize(document);
            await File.WriteAllTextAsync(filePath, json);

            Console.WriteLine();
            Console.WriteLine($"✓ Template created: {filePath}");
            Console.WriteLine();
            Console.WriteLine("The template has been created with one example section and prompt.");
            Console.WriteLine("Edit the file to add more sections and prompts.");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
