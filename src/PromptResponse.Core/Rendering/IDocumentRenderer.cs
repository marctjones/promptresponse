using PromptResponse.Core.Models;

namespace PromptResponse.Core.Rendering;

/// <summary>
/// Renders an <see cref="AprDocument"/> to a concrete output format.
/// </summary>
/// <remarks>
/// This is the seam that lets PDF, plain text, HTML, and print share one
/// document traversal: implementations are expected to flatten the document via
/// <see cref="DocumentRenderModelBuilder"/> (the single shared walk) and then
/// serialize the resulting <see cref="RenderModel"/> in their own format.
/// The contract is byte-oriented so the same interface serves both text formats
/// (which write UTF-8) and binary formats such as PDF.
/// </remarks>
public interface IDocumentRenderer
{
    /// <summary>
    /// A stable, lowercase identifier for the format (e.g. "text", "pdf", "html").
    /// </summary>
    string FormatId { get; }

    /// <summary>
    /// The file extension this renderer produces, including the leading dot
    /// (e.g. ".txt", ".pdf").
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Renders <paramref name="document"/> to <paramref name="output"/>.
    /// </summary>
    /// <param name="document">The document to render.</param>
    /// <param name="options">Content options shared across formats.</param>
    /// <param name="output">The stream the rendered bytes are written to.</param>
    void Render(AprDocument document, RenderOptions options, Stream output);
}

/// <summary>
/// Convenience helpers for <see cref="IDocumentRenderer"/>.
/// </summary>
public static class DocumentRendererExtensions
{
    /// <summary>
    /// Renders a document to a byte array using
    /// <see cref="RenderOptions.Default"/> when no options are supplied.
    /// </summary>
    public static byte[] RenderToBytes(
        this IDocumentRenderer renderer,
        AprDocument document,
        RenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        using var stream = new MemoryStream();
        renderer.Render(document, options ?? RenderOptions.Default, stream);
        return stream.ToArray();
    }
}
