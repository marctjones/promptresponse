using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Cli.Api.Filling;

/// <summary>
/// Persists filled forms using the APR filled-form file extension.
/// </summary>
internal sealed class FilledFormWriter(IAprSerializer serializer)
{
    public async Task<string> WriteAsync(AprDocument filledForm, string outputPath)
    {
        var resolvedPath = EnsureFilledFormExtension(outputPath);
        await File.WriteAllTextAsync(resolvedPath, serializer.Serialize(filledForm));
        return resolvedPath;
    }

    internal static string EnsureFilledFormExtension(string outputPath) =>
        outputPath.EndsWith(".aprf", StringComparison.OrdinalIgnoreCase)
            ? outputPath
            : Path.ChangeExtension(outputPath, ".aprf");
}
