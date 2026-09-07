using PromptResponse.Core.Beta6;
using PromptResponse.Core.Models;

namespace PromptResponse.Cli.Api.Filling;

/// <summary>
/// Persists filled forms, choosing JSONC or APR-YAML by the requested output
/// path's extension — the same convention the desktop client's
/// <c>AprDocumentPersistence</c> uses, so a filled form's representation follows
/// what the caller asked for rather than always being JSONC.
/// </summary>
internal sealed class FilledFormWriter
{
    private readonly AprBeta6Reader _reader = new();

    public async Task<string> WriteAsync(AprDocument filledForm, string outputPath)
    {
        var resolvedPath = EnsureFilledFormExtension(outputPath);
        var representation = RepresentationFor(resolvedPath);
        await File.WriteAllTextAsync(resolvedPath, _reader.WriteForm(filledForm, representation));
        return resolvedPath;
    }

    /// <summary>
    /// A YAML output path is left exactly as given — ".yaml"/".yml"/".apr.yaml" all
    /// already say what they mean. Anything else is defaulted to ".aprf", the same
    /// behaviour this had before YAML output existed.
    /// </summary>
    internal static string EnsureFilledFormExtension(string outputPath) =>
        RepresentationFor(outputPath) == AprRepresentation.Yaml || outputPath.EndsWith(".aprf", StringComparison.OrdinalIgnoreCase)
            ? outputPath
            : Path.ChangeExtension(outputPath, ".aprf");

    internal static AprRepresentation RepresentationFor(string path) =>
        path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
            ? AprRepresentation.Yaml
            : AprRepresentation.Jsonc;
}
