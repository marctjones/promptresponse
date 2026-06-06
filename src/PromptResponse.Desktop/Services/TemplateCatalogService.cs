using PromptResponse.Core.Serialization;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Loads the starter templates the build copies into a <c>Templates/</c> folder
/// next to the binary. Reads each template's title from its metadata.
/// </summary>
public sealed class TemplateCatalogService : ITemplateCatalogService
{
    private readonly List<StarterTemplate> _templates = new();

    public TemplateCatalogService(IAprSerializer serializer, string? templatesDirectory = null)
    {
        var dir = templatesDirectory ?? Path.Combine(AppContext.BaseDirectory, "Templates");
        if (!Directory.Exists(dir))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(dir, "*.aprt"))
        {
            try
            {
                var doc = serializer.Deserialize(File.ReadAllText(file));
                var title = string.IsNullOrWhiteSpace(doc.Metadata.Title)
                    ? Path.GetFileNameWithoutExtension(file)
                    : doc.Metadata.Title;
                _templates.Add(new StarterTemplate(title, file, doc.Metadata.Description));
            }
            catch
            {
                // A malformed bundled template shouldn't crash the home screen; skip it.
            }
        }

        _templates.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public IReadOnlyList<StarterTemplate> Templates => _templates;
}
