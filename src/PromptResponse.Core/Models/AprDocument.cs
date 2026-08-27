using System.Text.Json;
using System.Text.Json.Serialization;
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
    /// Members present in the source JSON that this build does not recognise.
    /// </summary>
    /// <remarks>
    /// Captured on read and written back out unchanged, so a document produced by a
    /// newer minor version of the format survives a round-trip through this build
    /// instead of being silently stripped. This is what makes additive format change
    /// possible; see <see cref="AprFormat"/>.
    ///
    /// Not covered by signatures — the canonical payload enumerates known fields only,
    /// so extension members on a signed document can be altered without invalidating
    /// the signature. Not sanitised either: the text rules apply to known string fields.
    /// </remarks>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }

    /// <summary>
    /// Gets or sets the format version of this APR document.
    /// </summary>
    /// <remarks>
    /// Used to ensure compatibility when the format evolves.
    /// Current version is <see cref="AprFormat.CurrentVersion"/>.
    /// </remarks>
    public string Version { get; set; } = AprFormat.CurrentVersion;

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

    /// <summary>
    /// Gets or sets the detached cryptographic signatures over this document.
    /// </summary>
    /// <remarks>
    /// Pure data — verification is a side-effect-free computation, consistent with
    /// the "no code execution, safe to open untrusted" principle. A publisher
    /// signature attests to the form definition (and binds the submission URL); a
    /// filler signature attests to the responses in its scope. Editing a covered
    /// field invalidates the signatures that cover it. See
    /// <c>PromptResponse.Core.Signing</c>. Null/absent when the document is
    /// unsigned, so unsigned files carry no signatures field.
    /// </remarks>
    public List<Signature>? Signatures { get; set; }
}
