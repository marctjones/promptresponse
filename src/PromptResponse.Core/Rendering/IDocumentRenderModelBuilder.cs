using PromptResponse.Core.Models;

namespace PromptResponse.Core.Rendering;

/// <summary>
/// Flattens an <see cref="AprDocument"/> into a layout-free
/// <see cref="RenderModel"/>. This is the single document traversal shared by
/// every output format (PDF, plain text, HTML, print).
/// </summary>
public interface IDocumentRenderModelBuilder
{
    /// <summary>
    /// Builds the flattened render model for <paramref name="document"/>.
    /// </summary>
    /// <param name="document">The document to flatten.</param>
    /// <param name="options">Content options (e.g. whether to include empty fields).</param>
    /// <returns>An ordered, layout-free model ready for rendering.</returns>
    RenderModel Build(AprDocument document, RenderOptions options);
}
