using PromptResponse.Cli.Commands.Reporting;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Cli.Commands;

/// <summary>Loads a document and delegates its human-readable report to a presenter.</summary>
public class InfoCommand : ICommand
{
    private readonly IAprSerializer _serializer;
    public InfoCommand(IAprSerializer serializer) => _serializer = serializer;

    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0) { Console.Error.WriteLine("Error: File path required"); Console.Error.WriteLine("Usage: apr info <file>"); return 1; }
        var filePath = args[0];
        if (!File.Exists(filePath)) { Console.Error.WriteLine($"Error: File not found: {filePath}"); return 1; }
        try
        {
            var document = _serializer.Deserialize(await File.ReadAllTextAsync(filePath));
            InfoDocumentPresenter.Write(document, filePath);
            SignatureNotice.Write(document);
            return 0;
        }
        catch (SerializationException ex) { Console.Error.WriteLine($"Error: Failed to load APR file: {ex.Message}"); return 1; }
        catch (Exception ex) { Console.Error.WriteLine($"Error: {ex.Message}"); return 1; }
    }
}
