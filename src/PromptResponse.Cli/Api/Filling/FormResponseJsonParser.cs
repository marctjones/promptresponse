using System.Text.Json;

namespace PromptResponse.Cli.Api.Filling;

/// <summary>
/// Parses the compact prompt-ID-to-response JSON payload accepted by the CLI API.
/// </summary>
internal static class FormResponseJsonParser
{
    public static Dictionary<string, string> Parse(string jsonResponses) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(jsonResponses)
        ?? throw new InvalidOperationException("Invalid JSON format");
}
