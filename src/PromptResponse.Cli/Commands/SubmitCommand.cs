using System.Net.Http.Headers;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;

namespace PromptResponse.Cli.Commands;

/// <summary>Explicitly posts a completed APR document to one HTTPS submission target.</summary>
public sealed class SubmitCommand(IAprSerializer serializer, DocumentValidator validator) : ICommand
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0) { Console.Error.WriteLine("Usage: apr submit <file.aprf> [--url=https://…] --yes"); return 1; }
        var file = args[0];
        var url = args.FirstOrDefault(a => a.StartsWith("--url=", StringComparison.Ordinal))?[6..];
        if (!File.Exists(file)) { Console.Error.WriteLine("Error: File not found."); return 1; }
        var document = serializer.Deserialize(await File.ReadAllTextAsync(file));
        if (!validator.Validate(document).IsValid) { Console.Error.WriteLine("Error: document has structural validation errors."); return 1; }
        var choices = document.Metadata.SubmissionUrls?.Where(IsHttps).ToList() ?? [];
        url ??= choices.Count == 1 ? choices[0] : null;
        if (url is null || !IsHttps(url)) { Console.Error.WriteLine("Error: specify one HTTPS target with --url=…; no automatic fallback is used."); return 1; }
        if (document.Metadata.SubmissionUrls is { Count: > 0 } && !document.Metadata.SubmissionUrls.Contains(url, StringComparer.Ordinal)) { Console.Error.WriteLine("Error: --url must be one of metadata.submissionUrls."); return 1; }
        if (!args.Contains("--yes", StringComparer.Ordinal)) { Console.Error.WriteLine($"Will POST {Path.GetFileName(file)} to {url}. Re-run with --yes to confirm."); return 2; }
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
        using var content = new StringContent(serializer.Serialize(document)); content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.apr+json");
        try
        {
            using var response = await client.PostAsync(url, content);
            if (!response.IsSuccessStatusCode) { Console.Error.WriteLine($"Submission failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. No redirect was followed."); return 1; }
            Console.WriteLine($"Submitted to {url}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}"); return 0;
        }
        catch (HttpRequestException ex) { Console.Error.WriteLine($"Submission failed: {ex.Message}"); return 1; }
    }
    private static bool IsHttps(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment);
}
