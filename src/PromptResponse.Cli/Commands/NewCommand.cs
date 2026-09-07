using PromptResponse.Core;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Models;

namespace PromptResponse.Cli.Commands;

/// <summary>
/// Creates a new APR template file.
/// </summary>
public class NewCommand : ICommand
{
    private readonly AprBeta6Reader _reader = new();

    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: File path required");
            Console.Error.WriteLine("Usage: apr new <file>");
            return 1;
        }

        var filePath = args[0];

        // Ensure APR extension (.aprt for templates by default). A YAML extension is
        // left exactly as given -- it already says both "this is APR" and which
        // representation to write.
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension != ".apr" && extension != ".aprt" && extension != ".aprf"
            && extension != ".yaml" && extension != ".yml")
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
                Version = AprFormat.CurrentVersion,
                DocumentType = DocumentType.Template,
                Metadata = new Metadata
                {
                    Title = title,
                    Description = string.IsNullOrWhiteSpace(description) ? null : description,
                    Author = string.IsNullOrWhiteSpace(author) ? null : author,
                    TemplateId = string.IsNullOrWhiteSpace(templateId) ? null : templateId,
                    TemplateVersion = AprFormat.CurrentVersion,
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

            // Serialize and save, in whichever representation the extension names.
            var representation = extension is ".yaml" or ".yml" ? AprRepresentation.Yaml : AprRepresentation.Jsonc;
            var text = _reader.WriteForm(document, representation);
            await File.WriteAllTextAsync(filePath, text);

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
