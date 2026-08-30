namespace PromptResponse.Cli.Commands.Diff;

/// <summary>One observable difference between two APR documents.</summary>
internal sealed record Difference(string Type, string Path, string? Value1, string? Value2);
