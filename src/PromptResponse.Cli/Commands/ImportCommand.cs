using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using PromptResponse.Rendering.Pdf;

namespace PromptResponse.Cli.Commands;

/// <summary>
/// Imports a fillable PDF (AcroForm) into an APR template (<c>.aprt</c>).
/// </summary>
/// <remarks>
/// This is the deterministic, machine-readable path: it reads the PDF's real form
/// fields. For flat/printed/scanned PDFs (no form fields), or Word/image inputs,
/// use the <c>document-to-apr</c> skill instead.
/// </remarks>
public class ImportCommand : ICommand
{
    private readonly IAprSerializer _serializer;
    private readonly DocumentValidator _validator;

    public ImportCommand(IAprSerializer serializer, DocumentValidator validator)
    {
        _serializer = serializer;
        _validator = validator;
    }

    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: PDF path required");
            Console.Error.WriteLine("Usage: apr import <file.pdf> [--output=<file.aprt>] [--title=<title>] [--report]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Imports a fillable PDF (AcroForm) into an APR template.");
            Console.Error.WriteLine("For flat/scanned PDFs, Word, or images, use the document-to-apr skill.");
            return 1;
        }

        var inputPath = args.FirstOrDefault(a => !a.StartsWith("--"));
        var outputPath = GetArgValue(args, "--output");
        var title = GetArgValue(args, "--title");
        var report = args.Contains("--report");

        if (string.IsNullOrEmpty(inputPath))
        {
            Console.Error.WriteLine("Error: PDF path required");
            return 1;
        }

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Error: File not found: {inputPath}");
            return 1;
        }

        outputPath ??= Path.ChangeExtension(inputPath, ".aprt");

        try
        {
            var importer = new PdfFormImporter();
            var (document, quality) = importer.ImportWithQuality(inputPath, title);

            var promptCount = CountPrompts(document.Sections);
            Console.WriteLine($"Imported '{document.Metadata.Title}' from {inputPath}");
            Console.WriteLine($"  Sections: {document.Sections.Count}");
            Console.WriteLine($"  Prompts:  {promptCount}");

            var result = _validator.Validate(document);
            if (!result.IsValid)
            {
                Console.Error.WriteLine($"Warning: imported document has {result.Errors.Count} validation error(s):");
                foreach (var error in result.Errors.Take(10))
                {
                    Console.Error.WriteLine($"  - {error.Message}");
                }
            }
            else
            {
                Console.WriteLine("  Validation: passed");
            }

            // Always print the one-line quality verdict; --report adds the breakdown.
            Console.WriteLine($"  Quality:    {quality.Summary}");
            if (report)
            {
                PrintReport(quality);
            }

            var json = _serializer.Serialize(document);
            await File.WriteAllTextAsync(outputPath, json);
            Console.WriteLine($"Wrote: {outputPath}");
            return 0;
        }
        catch (PdfFormImporter.NoFormFieldsException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: failed to import PDF: {ex.Message}");
            return 1;
        }
    }

    private static void PrintReport(PromptResponse.Rendering.Pdf.ImportQuality q)
    {
        Console.WriteLine();
        Console.WriteLine("Import quality report");
        Console.WriteLine($"  Score:             {q.Score}/100 ({q.Grade})");
        Console.WriteLine($"  Recommendation:    {q.Recommendation}");
        Console.WriteLine($"  Tooltip coverage:  {q.TooltipCoverage:P0}");
        Console.WriteLine($"  Cryptic labels:    {q.CrypticLabelRatio:P0}");
        Console.WriteLine($"  Duplicate labels:  {q.DuplicateLabelRatio:P0}");
        Console.WriteLine($"  Flags:             {q.Flags.Count}");

        const int show = 15;
        foreach (var flag in q.Flags.Take(show))
        {
            Console.WriteLine($"    - [{flag.Kind}] {flag.PromptId}: {flag.Message}");
        }
        if (q.Flags.Count > show)
        {
            Console.WriteLine($"    … and {q.Flags.Count - show} more.");
        }
    }

    private static int CountPrompts(IEnumerable<Core.Models.Section> sections)
    {
        var count = 0;
        foreach (var s in sections)
        {
            count += s.Prompts.Count + CountPrompts(s.Sections);
        }
        return count;
    }

    private static string? GetArgValue(string[] args, string prefix)
    {
        var arg = args.FirstOrDefault(a => a.StartsWith(prefix + "="));
        return arg?.Substring(prefix.Length + 1);
    }
}
