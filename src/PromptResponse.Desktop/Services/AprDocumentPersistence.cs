using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Desktop.Services;

/// <summary>Owns APR stream persistence independently from Avalonia picker state.</summary>
internal sealed class AprDocumentPersistence(IAprSerializer serializer)
{
    internal async Task<AprDocument?> LoadAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;
        await using var stream = File.OpenRead(filePath);
        return await serializer.DeserializeAsync(stream);
    }

    internal async Task SaveAsync(AprDocument document, string filePath)
    {
        document.Metadata.Modified = DateTime.UtcNow;
        await using var stream = File.Create(filePath);
        await serializer.SerializeAsync(document, stream);
    }
}
