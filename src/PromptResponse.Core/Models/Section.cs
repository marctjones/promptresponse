using System.Text.Json;
using System.Text.Json.Serialization;
namespace PromptResponse.Core.Models;

/// <summary>
/// Represents a section in an APR document with unlimited nesting capability.
/// </summary>
/// <remarks>
/// Sections provide semantic grouping of related prompts and can nest to any depth.
/// A section can contain both child sections and direct prompts, allowing
/// for flexible organization without enforcing a rigid structure.
/// There is no limit to how deeply sections can be nested.
/// </remarks>
public class Section
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
    /// Gets or sets the unique identifier for this section.
    /// </summary>
    /// <remarks>
    /// IDs must be unique within the document and should remain stable across versions.
    /// Recommended format: "section_001", "section_001_001", etc.
    /// </remarks>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of this section.
    /// </summary>
    /// <example>
    /// "Personal Information", "Employment History", "References"
    /// </example>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional description for this section.
    /// </summary>
    /// <remarks>
    /// Provides additional context or instructions for this section.
    /// </remarks>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the list of child sections within this section.
    /// </summary>
    /// <remarks>
    /// Child sections provide nested grouping within the section.
    /// Sections can be nested to any depth without limit.
    /// Optional - sections can have only direct prompts without child sections.
    /// </remarks>
    public List<Section> Sections { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of prompts directly in this section.
    /// </summary>
    /// <remarks>
    /// A section's own prompts are presented BEFORE its child sections, matching
    /// document convention: a heading's own content precedes its subheadings.
    /// A section can have both child sections and direct prompts.
    /// Prompts are displayed in the order they appear in this list.
    /// This ordering is normative — see the specification, section 10.2.
    /// </remarks>
    public List<Prompt> Prompts { get; set; } = new();

    /// <summary>
    /// What kind of section this is. Absent or <c>"section"</c> for an ordinary
    /// section; <c>"table"</c> when this section's child sections are repeating
    /// instances of one shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>"table"</c> is a claim about structure, not appearance.</b> It asserts
    /// that the child sections are instances rather than free-standing subsections,
    /// that prompts at the same position correspond across those instances, that each
    /// instance's <see cref="Title"/> identifies it, and that a prompt's
    /// <c>Label</c> names the corresponding field across every instance.
    /// </para>
    /// <para>
    /// It licenses no layout data whatsoever. A renderer may present it as a grid, as
    /// stacked cards, as a flat sequence of prompts, or as speech — all are equally
    /// conformant, and the linear presentations are the right choice on a narrow
    /// screen or for a screen-reader profile, not a degraded fallback.
    /// </para>
    /// <para>
    /// Rows are ordinary <see cref="Section"/>s and cells are ordinary
    /// <see cref="Prompt"/>s, with no special rules of their own — that is the point.
    /// A table therefore introduces no new primitive, and there is nowhere for a
    /// column definition to drift out of step with the cells it describes.
    /// </para>
    /// </remarks>
    public string? Kind { get; set; }

    /// <summary>
    /// Who is meant to fill this in - "patient", "nurse", "office" - or null for anyone.
    /// </summary>
    /// <remarks>
    /// A statement of intent, never enforcement. The format has no idea who is at the
    /// keyboard, so a reader marks a field as somebody else's and still lets it be typed
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
    /// Whether a filler may add or remove instances. String <c>"true"</c> to allow it;
    /// absent or anything else means instances are fixed.
    /// </summary>
    /// <remarks>
    /// Meaningful only when <see cref="Kind"/> is <c>"table"</c>. The default is
    /// deliberately restrictive: a fixed table that silently gained a row (a fifth
    /// quarter, a extra tax year) is a worse failure than a line-item table needing
    /// one explicit property.
    ///
    /// This is independent of whether the instances hold values. A filled table whose
    /// rows carry data may still accept new rows.
    /// </remarks>
    public string? CanAddRows { get; set; }

    /// <summary>
    /// Advisory upper bound on the number of instances, as a string.
    /// </summary>
    /// <remarks>
    /// A hint for the add-row affordance, never a reason to reject a document: a table
    /// carrying more instances than this is still valid and is reported as a warning.
    /// A string, like every other value in the format.
    /// </remarks>
    public string? MaxRows { get; set; }

    /// <summary>Whether this section's child sections are repeating instances.</summary>
    /// <remarks>
    /// Not serialized. It is derived from <see cref="Kind"/>, and System.Text.Json writes
    /// public getters by default - so without this it reached the wire as a JSON boolean,
    /// in a format whose strings-only rule permits exactly one (specification 3.2), and as
    /// a second copy of a fact <c>kind</c> already carries. Two copies of one fact is a
    /// correctness bug everywhere in this format; here it was also a conformance one.
    /// </remarks>
    [JsonIgnore]
    public bool IsTable => string.Equals(Kind, "table", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether a filler may add or remove instances of this table.</summary>
    /// <remarks>Not serialized, for the same reason as <see cref="IsTable"/>.</remarks>
    [JsonIgnore]
    public bool AllowsAddingRows =>
        IsTable && string.Equals(CanAddRows, "true", StringComparison.OrdinalIgnoreCase);

}
