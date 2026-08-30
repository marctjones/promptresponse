namespace PromptResponse.Cli.Commands.Fill;

/// <summary>
/// Parses and names the options accepted by the fill command.
/// </summary>
internal sealed class FillCommandOptions
{
    private const string JsonFileOption = "--json-file";
    private const string JsonOption = "--json";
    private const string NonInteractiveOption = "--non-interactive";
    private const string OutputOption = "--output";
    private const string FilledByOption = "--filled-by";
    private const string ValidateOption = "--validate";

    private readonly Dictionary<string, string> _values;

    private FillCommandOptions(Dictionary<string, string> values) => _values = values;

    internal string? JsonFilePath => GetValue(JsonFileOption);
    internal string? Json => GetValue(JsonOption);
    internal string? OutputPath => GetValue(OutputOption);
    internal string? FilledBy => GetValue(FilledByOption);
    internal bool Validate => HasOption(ValidateOption);
    internal bool IsNonInteractive => HasOption(NonInteractiveOption);
    internal IReadOnlyDictionary<string, string> Values => _values;

    internal static FillCommandOptions Parse(IEnumerable<string> arguments)
    {
        var values = new Dictionary<string, string>();

        foreach (var argument in arguments)
        {
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = argument.Split('=', 2);
            values[parts[0]] = parts.Length == 2 ? parts[1] : "true";
        }

        return new FillCommandOptions(values);
    }

    internal bool HasOption(string option) => _values.ContainsKey(option);

    internal string? GetValue(string option) => _values.GetValueOrDefault(option);
}
