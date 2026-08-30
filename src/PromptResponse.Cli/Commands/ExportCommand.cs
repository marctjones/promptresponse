using PromptResponse.Core.Serialization;
using PromptResponse.Cli.Commands.Export;

namespace PromptResponse.Cli.Commands;

/// <summary>
/// Exports responses from an APR file to various formats.
/// </summary>
public class ExportCommand : ICommand
{
    private readonly IAprSerializer _serializer;

    public ExportCommand(IAprSerializer serializer)
    {
        _serializer = serializer;
    }

    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: File path required");
            Console.Error.WriteLine("Usage: apr export <file> [--format=<csv|json|txt|html|pdf>] [--output=<file>] [--exclude-empty] [--fillable]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Formats:");
            Console.Error.WriteLine("  csv  - Comma-separated values (default)");
            Console.Error.WriteLine("  json - JSON format with prompt/response pairs");
            Console.Error.WriteLine("  txt  - Plain text format");
            Console.Error.WriteLine("  html - Accessible HTML page (--fillable: interactive form that downloads .aprf)");
            Console.Error.WriteLine("  pdf  - Flattened PDF (requires --output)");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --exclude-empty       Omit unanswered fields (pdf)");
            Console.Error.WriteLine("  --fillable            Export a fillable form with live fields (pdf, html)");
            Console.Error.WriteLine("  --page-size=<size>    letter (default), a4, or legal (pdf)");
            Console.Error.WriteLine("  --banner=<text>       Classification/handling banner on every page (pdf), e.g. \"OFFICIAL\"");
            Console.Error.WriteLine("  --pdfa                Produce a PDF/A-2b archival file (flat, embedded font) (pdf)");
            return 1;
        }

        if (!ExportRequest.TryParse(args, out var request, out var parseError))
        {
            Console.Error.WriteLine(parseError);
            if (parseError!.Contains("Unsupported format", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Supported formats: csv, json, txt, html, pdf");
            }
            return 1;
        }

        if (!File.Exists(request!.InputPath))
        {
            Console.Error.WriteLine($"Error: File not found: {request.InputPath}");
            return 1;
        }

        try
        {
            // Load document
            var json = await File.ReadAllTextAsync(request.InputPath);
            var document = _serializer.Deserialize(json);

            if (request.Format == ExportFormat.Pdf)
            {
                return await PdfExportWriter.WriteAsync(document, request);
            }

            var exportContent = ExportContentRenderer.Render(document, request.Format, request.Fillable);

            await ExportOutputWriter.WriteAsync(exportContent, request.OutputPath);
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
