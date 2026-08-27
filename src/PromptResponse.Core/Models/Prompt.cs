using System.Text.Json;
using System.Text.Json.Serialization;
namespace PromptResponse.Core.Models;

/// <summary>
/// Represents a single prompt (question/field) in an APR document.
/// </summary>
/// <remarks>
/// Prompts are the fundamental unit of data collection in APR documents.
/// Responses are always stored as strings, regardless of the expected data type.
/// This ensures maximum portability and prevents data loss from type coercion.
/// </remarks>
public class Prompt
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

    private string _response = string.Empty;

    /// <summary>
    /// Gets or sets the unique identifier for this prompt.
    /// </summary>
    /// <remarks>
    /// IDs must be unique within the document and should remain stable across versions.
    /// Recommended format: "prompt_001", "prompt_002", etc.
    /// </remarks>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user-visible label for this prompt.
    /// </summary>
    /// <example>
    /// "Full Legal Name", "Email Address", "Date of Birth"
    /// </example>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the response value.
    /// </summary>
    /// <remarks>
    /// Always stored as a string regardless of expected data type.
    /// Setting this property automatically updates ResponseMetadata.LastModified.
    /// Null values are converted to empty strings.
    /// </remarks>
    public string Response
    {
        get => _response;
        set
        {
            _response = value ?? string.Empty;
            ResponseMetadata.LastModified = DateTime.UtcNow;
            // Any write that is not a recomputation makes this an authored answer.
            // FormExpressions re-marks it afterwards when it wrote the value itself.
            ResponseMetadata.Source = null;
        }
    }

    /// <summary>
    /// Replaces the response text without recording the write as an answer.
    /// </summary>
    /// <remarks>
    /// Normalizing is not answering. Sanitization runs on every load and every save, and
    /// it assigned through the Response setter above - so opening a file restamped
    /// LastModified on every prompt and cleared Source on every prompt, whether or not
    /// the text changed at all.
    ///
    /// Two things were lost. LastModified stopped meaning "when this answer changed" and
    /// started meaning "when the file was last opened or saved". And Source - the marker
    /// saying whether a value was computed or typed by a person - was erased, which is
    /// what tells a recomputation to leave a hand-corrected value alone. A round trip
    /// through disk would have made every corrected computed field look computed again.
    /// </remarks>
    internal void SetNormalizedResponse(string? value) => _response = value ?? string.Empty;

    /// <summary>
    /// Gets or sets hints for how this prompt should be presented and validated.
    /// </summary>
    public PromptHints Hints { get; set; } = new();

    /// <summary>
    /// Who is meant to fill this in - "patient", "nurse", "office" - or null for anyone.
    /// </summary>
    /// <remarks>
    /// Overrides the role of the section containing it, and is a statement of intent
    /// rather than enforcement. The format has no idea who is at the keyboard, so a
    /// reader marks a field as somebody else's and still lets it be typed
    /// into (specification 4.10). Any string is a valid response, and that does not stop
    /// being true because the field was labelled for the office.
    ///
    /// Accountability comes afterwards, from signatures: a scoped filler signature over
    /// those fields, made with the nurse's certificate, is evidence the nurse filled them.
    /// A greyed-out box is not evidence of anything.
    ///
    /// An open vocabulary. Roles are domain-specific and a reader that does not recognise
    /// one shows the field normally rather than erroring.
    /// </remarks>
    public string? Role { get; set; }

    /// <summary>
    /// Gets or sets metadata about the response.
    /// </summary>
    /// <remarks>
    /// Primarily used in filled forms to track response information.
    /// </remarks>
    public ResponseMetadata ResponseMetadata { get; set; } = new();
}
