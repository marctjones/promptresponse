using PromptResponse.Core.Models;
using PromptResponse.Core.Beta6;

namespace PromptResponse.Desktop.Services;

/// <summary>Owns APR stream persistence independently from Avalonia picker state.</summary>
internal sealed class AprDocumentPersistence
{
    internal async Task<IReadOnlyList<AprStreamRecord>> LoadStreamAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return [];
        var source = await File.ReadAllTextAsync(filePath);
        return new AprBeta6Reader().ReadStream(source, RepresentationFor(filePath));
    }

    internal async Task<AprDocument?> LoadAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;
        // Beta.6 is the desktop file boundary. The preview exists only for the
        // IFileService single-document contract; DocumentSessionWorkflow reparses a
        // stream and requires occurrence selection before it is opened for editing.
        return (await LoadStreamAsync(filePath))
            .OfType<AprFormRecord>().FirstOrDefault()?.Form;
    }

    internal async Task SaveAsync(AprDocument document, string filePath)
    {
        document.Metadata.Modified = DateTime.UtcNow;
        var representation = RepresentationFor(filePath);
        await File.WriteAllTextAsync(filePath, new AprBeta6Reader().WriteForm(document, representation));
    }

    internal async Task SaveStreamAsync(IEnumerable<AprStreamRecord> records, string filePath)
    {
        var representation = RepresentationFor(filePath);
        await File.WriteAllTextAsync(filePath, new AprBeta6Reader().WriteStream(records, representation));
    }

    private static AprRepresentation RepresentationFor(string filePath) =>
        Path.GetExtension(filePath).Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(filePath).Equals(".yml", StringComparison.OrdinalIgnoreCase)
            ? AprRepresentation.Yaml : AprRepresentation.Jsonc;

}
