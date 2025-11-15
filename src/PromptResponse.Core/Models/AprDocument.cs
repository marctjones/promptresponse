namespace PromptResponse.Core.Models;

/// <summary>
/// Root document representing an APR (Adaptive Prompt Response) file.
/// </summary>
/// <remarks>
/// An APR document can be either a template (blank form) or a filled form (with responses).
/// The document contains metadata and a collection of sections that organize prompts.
/// </remarks>
public class AprDocument
{
    /// <summary>
    /// Gets or sets the format version of this APR document.
    /// </summary>
    /// <remarks>
    /// Used to ensure compatibility when the format evolves.
    /// Current version is "1.0".
    /// </remarks>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Gets or sets the type of document (Template or FilledForm).
    /// </summary>
    /// <remarks>
    /// Determines how the application handles the document when opened.
    /// Templates can be edited or filled out; FilledForms are opened for editing responses.
    /// </remarks>
    public DocumentType DocumentType { get; set; } = DocumentType.Template;

    /// <summary>
    /// Gets or sets the metadata for this document.
    /// </summary>
    /// <remarks>
    /// Contains title, description, timestamps, and other document-level information.
    /// Different fields are relevant for templates vs filled forms.
    /// </remarks>
    public Metadata Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of sections in this document.
    /// </summary>
    /// <remarks>
    /// Sections provide top-level organization of prompts.
    /// Sections are displayed in the order they appear in this list.
    /// A document must have at least one section (enforced by validation, not by this model).
    /// </remarks>
    public List<Section> Sections { get; set; } = new();
}
