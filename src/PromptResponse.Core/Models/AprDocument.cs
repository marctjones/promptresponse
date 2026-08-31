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
    /// <remarks>
    /// Required on the wire. The schema lists version, metadata and sections as required
    /// top-level members, and specification section 6.3 makes a structurally wrong shape a
    /// parse failure rather than a validation error - so an object carrying none of them
    /// must not deserialize into an empty document. It did, which meant `apr info` reported
    /// success on any JSON object at all, and a third-party implementation reading the
    /// schema would have rejected files this one accepted.
    ///
    /// JsonRequired affects deserialization only; constructing an AprDocument in code still
    /// uses the defaults below.
    /// </remarks>
    [JsonRequired]
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
    [JsonRequired]
    public Metadata Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of sections in this document.
    /// </summary>
    /// <remarks>
    /// Sections provide top-level organization of prompts.
    /// Sections are displayed in the order they appear in this list.
    /// A document must have at least one section (enforced by validation, not by this model).
    /// </remarks>
    [JsonRequired]
    public List<Section> Sections { get; set; } = new();

    /// <summary>
    /// The parties the author expects to fill parts of this form.
    /// </summary>
    /// <remarks>
    /// Optional, and optional to be complete. Section and prompt <c>role</c> members may
    /// reference an identifier that is not declared here; the vocabulary is open
    /// (specification 4.10) and a reader shows the bare identifier rather than erroring.
    /// Declaring a role gives it a name worth showing a person, which is what makes
    /// "which role are you filling?" a usable question.
    /// </remarks>
    public List<RoleDefinition>? Roles { get; set; }

}
