using System.Text.Json;
using System.Text.Json.Serialization;
namespace PromptResponse.Core.Models;

/// <summary>
/// Metadata about a prompt response in a filled form.
/// </summary>
/// <remarks>
/// This metadata is optional and primarily used in filled forms to track
/// information about how and when the response was provided.
/// </remarks>
public class ResponseMetadata
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
    /// Gets or sets the inferred or detected data type of the response.
    /// </summary>
    /// <remarks>
    /// This is an optional hint about what type of data was detected in the response.
    /// Common values: "text", "email", "date", "number", etc.
    /// </remarks>
    public string? InferredDataType { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the response was last modified.
    /// </summary>
    /// <remarks>
    /// Automatically updated when the response value changes.
    /// Stored as UTC timestamp.
    /// </remarks>
    public DateTime? LastModified { get; set; }
}
