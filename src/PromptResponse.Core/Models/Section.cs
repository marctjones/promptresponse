namespace PromptResponse.Core.Models;

/// <summary>
/// Represents a top-level section in an APR document.
/// </summary>
/// <remarks>
/// Sections provide semantic grouping of related prompts.
/// A section can contain both subsections and direct prompts, allowing
/// for flexible organization without enforcing a rigid structure.
/// </remarks>
public class Section
{
    /// <summary>
    /// Gets or sets the unique identifier for this section.
    /// </summary>
    /// <remarks>
    /// IDs must be unique within the document and should remain stable across versions.
    /// Recommended format: "section_001", "section_002", etc.
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
    /// Provides additional context or instructions for the entire section.
    /// </remarks>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the list of subsections within this section.
    /// </summary>
    /// <remarks>
    /// Subsections provide nested grouping within the section.
    /// Optional - sections can have only direct prompts without subsections.
    /// </remarks>
    public List<Subsection> Subsections { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of prompts directly in this section.
    /// </summary>
    /// <remarks>
    /// Prompts at the section level appear after all subsections in the UI.
    /// A section can have both subsections and direct prompts.
    /// Prompts are displayed in the order they appear in this list.
    /// </remarks>
    public List<Prompt> Prompts { get; set; } = new();
}
