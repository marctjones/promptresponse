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
    /// Prompts at the section level appear after all child sections in the UI.
    /// A section can have both child sections and direct prompts.
    /// Prompts are displayed in the order they appear in this list.
    /// </remarks>
    public List<Prompt> Prompts { get; set; } = new();

    /// <summary>
    /// Gets or sets the optional table layout for this section.
    /// </summary>
    /// <remarks>
    /// When set, this section is rendered as a table: column headers come from
    /// <see cref="TableDefinition.Columns"/>, each entry in <see cref="Sections"/>
    /// is a row, and each row's <see cref="Prompts"/> hold the cell values
    /// (one prompt per column, with <c>id = "{rowId}.{columnId}"</c>).
    ///
    /// Static tables: <see cref="TableDefinition.FixedRows"/> declares which row
    /// sub-sections must exist. Dynamic tables: <see cref="TableDefinition.DynamicRows"/>
    /// configures min/max bounds and the row label prefix; the user adds/removes
    /// row sub-sections at fill time.
    ///
    /// Cells are regular Prompts: every value is a string, type hints are advisory,
    /// any visible text is a valid response (vision invariants).
    /// </remarks>
    public TableDefinition? TableLayout { get; set; }
}
