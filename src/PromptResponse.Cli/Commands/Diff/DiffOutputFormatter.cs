namespace PromptResponse.Cli.Commands.Diff;

/// <summary>Renders comparison results without coupling comparison logic to the console.</summary>
internal static class DiffOutputFormatter
{
    private const string Rule = "═══════════════════════════════════════";

    internal static IEnumerable<string> Format(string firstFileName, string secondFileName, IReadOnlyList<Difference> differences)
    {
        yield return Rule;
        yield return "Document Comparison";
        yield return Rule;
        yield return $"File 1: {firstFileName}";
        yield return $"File 2: {secondFileName}";
        yield return string.Empty;

        if (differences.Count == 0)
        {
            yield return "✓ Documents are identical (responses match)";
            yield return string.Empty;
            yield return Rule;
            yield break;
        }

        yield return $"Found {differences.Count} difference(s):";
        yield return string.Empty;
        foreach (var difference in differences)
        {
            yield return $"[{difference.Type}] {difference.Path}";
            yield return $"  File 1: {difference.Value1 ?? "(empty)"}";
            yield return $"  File 2: {difference.Value2 ?? "(empty)"}";
            yield return string.Empty;
        }

        yield return Rule;
        yield return $"Summary: {differences.Count} difference(s) found";
        yield return Rule;
    }
}
