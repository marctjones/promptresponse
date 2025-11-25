using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;

namespace PromptResponse.Cli.Commands;

/// <summary>
/// Validates an APR file.
/// </summary>
public class ValidateCommand : ICommand
{
    private readonly IAprSerializer _serializer;
    private readonly DocumentValidator _documentValidator;
    private readonly DataTypeValidator _dataTypeValidator;

    public ValidateCommand(
        IAprSerializer serializer,
        DocumentValidator documentValidator,
        DataTypeValidator dataTypeValidator)
    {
        _serializer = serializer;
        _documentValidator = documentValidator;
        _dataTypeValidator = dataTypeValidator;
    }

    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: File path required");
            Console.Error.WriteLine("Usage: apr validate <file>");
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
            Console.WriteLine($"Validating: {filePath}");
            var json = await File.ReadAllTextAsync(filePath);
            var document = _serializer.Deserialize(json);

            // Validate structure
            var structureResult = _documentValidator.Validate(document);

            // Validate data types (advisory)
            var dataTypeResult = _dataTypeValidator.ValidateDocument(document);

            // Report results
            if (structureResult.IsValid && dataTypeResult.IsValid)
            {
                Console.WriteLine("✓ Validation passed");
                Console.WriteLine($"  Document type: {document.DocumentType}");
                Console.WriteLine($"  Title: {document.Metadata.Title}");
                Console.WriteLine($"  Sections: {document.Sections.Count}");
                var promptCount = CountPrompts(document);
                Console.WriteLine($"  Prompts: {promptCount}");
                return 0;
            }

            // Show errors
            if (!structureResult.IsValid)
            {
                Console.WriteLine("✗ Structure validation failed:");
                foreach (var error in structureResult.Errors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }

            // Show warnings for data type mismatches
            if (!dataTypeResult.IsValid)
            {
                Console.WriteLine();
                Console.WriteLine("⚠ Data type warnings (advisory):");
                foreach (var error in dataTypeResult.Errors)
                {
                    Console.WriteLine($"  - {error}");
                }
                Console.WriteLine();
                Console.WriteLine("Note: Type mismatches are warnings only. All text input is valid.");
            }

            return structureResult.IsValid ? 0 : 1;
        }
        catch (SerializationException ex)
        {
            Console.Error.WriteLine($"✗ Serialization error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"✗ Unexpected error: {ex.Message}");
            return 1;
        }
    }

    private int CountPrompts(Core.Models.AprDocument document)
    {
        var count = 0;
        foreach (var section in document.Sections)
        {
            count += CountPromptsInSection(section);
        }
        return count;
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
}
