using PromptResponse.Core.Serialization;
using PromptResponse.Cli.Commands.Diff;

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
            var differences = DocumentDiffComparer.Compare(doc1, doc2);

            // Display results
            foreach (var line in DiffOutputFormatter.Format(
                         Path.GetFileName(file1Path),
                         Path.GetFileName(file2Path),
                         differences))
            {
                Console.WriteLine(line);
            }

            return differences.Count == 0 ? 0 : 1;
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
