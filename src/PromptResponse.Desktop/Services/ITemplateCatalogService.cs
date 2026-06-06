namespace PromptResponse.Desktop.Services;

/// <summary>
/// Provides the starter templates bundled with the app, shown on the home
/// screen as "start from a template" options.
/// </summary>
public interface ITemplateCatalogService
{
    /// <summary>The bundled starter templates, ordered by title.</summary>
    IReadOnlyList<StarterTemplate> Templates { get; }
}

/// <summary>A bundled starter template.</summary>
/// <param name="Title">The template's display title.</param>
/// <param name="Path">Absolute path to the <c>.aprt</c> file.</param>
/// <param name="Description">Optional one-line description.</param>
public sealed record StarterTemplate(string Title, string Path, string? Description);
