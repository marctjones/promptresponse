using System.Globalization;
using PromptResponse.Conversion.Pdf;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Conversion.Pdf.Tool;

/// <summary>
/// A standalone <c>convert-pdf</c>, deliberately outside the <c>apr</c> CLI.
/// </summary>
/// <remarks>
/// Kept separate while the pipeline is being built so an unfinished converter
/// cannot regress a shipping command. Folding it into <c>apr</c> is a decision
/// to take once the phases work.
/// </remarks>
public static class Program
{
    /// <summary>Entry point.</summary>
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintUsage();
            return args.Length == 0 ? 2 : 0;
        }

        var input = args[0];
        var output = ValueOf(args, "--output");
        var title = ValueOf(args, "--title");
        var quiet = args.Contains("--quiet");

        if (!File.Exists(input))
        {
            Console.Error.WriteLine($"Error: file not found: {input}");
            return 1;
        }

        var result = ConversionPipeline.Default().Convert(input, title);

        if (!quiet)
        {
            WriteReport(result);
        }

        if (!result.Succeeded)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "No template was produced. The phases that would have found fields on this " +
                "document are not built yet — see the report above for which.");
            return 1;
        }

        var destination = output ?? Path.ChangeExtension(input, ".aprt");
        File.WriteAllText(destination, new AprJsonSerializer().Serialize(result.State.Document!));
        Console.WriteLine();
        Console.WriteLine($"Wrote {destination}");
        return 0;
    }

    private static void WriteReport(ConversionResult result)
    {
        Console.WriteLine($"convert-pdf: {result.State.SourcePath}");
        Console.WriteLine();
        Console.WriteLine($"  {"phase",-22} {"status",-16} {"fields",6} {"labelled",9}  detail");
        Console.WriteLine($"  {new string('-', 22)} {new string('-', 16)} {new string('-', 6)} {new string('-', 9)}  {new string('-', 40)}");

        foreach (var phase in result.Phases)
        {
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  {phase.Name,-22} {Describe(phase.Status),-16} {phase.FieldsAfter,6} {phase.LabelledAfter,9}  {phase.Detail}"));
        }

        Console.WriteLine();
        var unbuilt = result.Unbuilt;
        if (unbuilt.Count > 0)
        {
            // Said plainly rather than left to be inferred from the table: a
            // pipeline that looks like it ran eight steps when it ran three is
            // the more expensive kind of wrong.
            Console.WriteLine($"  {unbuilt.Count} of {result.Phases.Count} phases are not built yet: " +
                              string.Join(", ", unbuilt.Select(p => p.Name)));
        }

        if (result.ReviewQueueSize > 0)
        {
            Console.WriteLine($"  {result.ReviewQueueSize} field(s) unresolved by the deterministic phases.");
        }
    }

    private static string Describe(PhaseStatus status) => status switch
    {
        PhaseStatus.Completed => "ok",
        PhaseStatus.Skipped => "skipped",
        PhaseStatus.NotImplemented => "NOT BUILT",
        PhaseStatus.Failed => "FAILED",
        _ => status.ToString(),
    };

    private static string? ValueOf(string[] args, string name)
    {
        var prefix = name + "=";
        var match = args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.Ordinal));
        return match?[prefix.Length..];
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: convert-pdf <file.pdf> [--output=<file.aprt>] [--title=<title>] [--quiet]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Converts a PDF form into an APR template, reporting what each phase did.");
        Console.Error.WriteLine("Standalone while the pipeline is built; it does not touch the `apr` CLI.");
        Console.Error.WriteLine();
        foreach (var phase in ConversionPipeline.Default().Phases)
        {
            Console.Error.WriteLine($"  {phase.Name,-22} {phase.Purpose}");
        }
    }
}
