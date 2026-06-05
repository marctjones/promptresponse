using PromptResponse.Core.Models;

namespace PromptResponse.Core.Rendering;

/// <summary>
/// The flattened, layout-free representation of an <see cref="AprDocument"/>
/// ready for any output format to render.
/// </summary>
/// <remarks>
/// Produced once by <see cref="DocumentRenderModelBuilder"/> and consumed by
/// each <see cref="IDocumentRenderer"/>. This is the single shared traversal:
/// PDF, plain text, HTML, and print all render the same <see cref="Blocks"/>
/// sequence rather than each walking the document tree independently.
/// </remarks>
/// <param name="Title">The document title (from metadata).</param>
/// <param name="Description">Optional document description.</param>
/// <param name="DocumentType">Whether this is a template or a filled form.</param>
/// <param name="Blocks">The ordered, flattened content blocks.</param>
public sealed record RenderModel(
    string Title,
    string? Description,
    DocumentType DocumentType,
    IReadOnlyList<RenderBlock> Blocks);
