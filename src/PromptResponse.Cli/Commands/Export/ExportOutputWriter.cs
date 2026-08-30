namespace PromptResponse.Cli.Commands.Export;

/// <summary>Writes text exports consistently to stdout or to an explicit destination.</summary>
internal static class ExportOutputWriter
{
    internal static async Task WriteAsync(string content, string? outputPath)
    {
        if (string.IsNullOrEmpty(outputPath))
        {
            Console.WriteLine(content);
            return;
        }

        await File.WriteAllTextAsync(outputPath, content);
        Console.WriteLine($"Exported to: {outputPath}");
    }
}
