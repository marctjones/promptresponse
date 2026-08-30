using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using PromptResponse.Rendering.Pdf;

namespace PromptResponse.Cli.Commands.Export;

/// <summary>Owns the binary PDF output policy for the export command.</summary>
internal static class PdfExportWriter
{
    internal static async Task<int> WriteAsync(AprDocument document, ExportRequest request)
    {
        // A PDF is binary, so it must be written to a file rather than stdout.
        if (string.IsNullOrEmpty(request.OutputPath))
        {
            Console.Error.WriteLine("Error: PDF export requires an output file. Use --output=<file>.");
            return 1;
        }

        if (request.Archival && request.Fillable)
        {
            Console.Error.WriteLine("Note: --pdfa produces a flat archival PDF; --fillable is ignored.");
        }

        var print = new PdfRenderOptions
        {
            PageSize = request.PageSize,
            BannerText = request.Banner,
            Archival = request.Archival,
        };
        IDocumentRenderer renderer = request.Fillable && !request.Archival
            ? new FillablePdfDocumentRenderer(print: print)
            : new PdfDocumentRenderer(print: print);

        await using var stream = File.Create(request.OutputPath);
        renderer.Render(document, new RenderOptions { IncludeEmptyFields = !request.ExcludeEmpty }, stream);

        Console.WriteLine($"Exported to: {request.OutputPath}");
        return 0;
    }
}
