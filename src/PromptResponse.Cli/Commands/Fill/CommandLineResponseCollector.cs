namespace PromptResponse.Cli.Commands.Fill;

/// <summary>
/// Extracts prompt responses from non-interactive fill options.
/// </summary>
internal static class CommandLineResponseCollector
{
    private const string ResponseOptionPrefix = "--set-";

    internal static Dictionary<string, string> Collect(IReadOnlyDictionary<string, string> options)
    {
        var responses = new Dictionary<string, string>();

        foreach (var (key, value) in options)
        {
            if (key.StartsWith(ResponseOptionPrefix, StringComparison.Ordinal))
            {
                responses[key[ResponseOptionPrefix.Length..]] = value;
            }
        }

        return responses;
    }
}
