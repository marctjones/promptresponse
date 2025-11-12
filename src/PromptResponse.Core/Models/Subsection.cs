namespace PromptResponse.Core.Models;

/// <summary>
/// Represents a subsection within a section of an APR document.
/// </summary>
/// <remarks>
/// Subsections provide nested grouping within sections for better organization.
/// Subsections can only contain prompts, not additional nested subsections.
/// Maximum nesting depth is: Section → Subsection → Prompt (3 levels).
/// </remarks>
public class Subsection
{
    /// <summary>
    /// Gets or sets the unique identifier for this subsection.
    /// </summary>
    /// <remarks>
    /// IDs must be unique within the document.
    /// Recommended format: "subsection_001_001", "subsection_001_002", etc.
    /// </remarks>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of this subsection.
    /// </summary>
    /// <example>
    /// "Name and Contact", "Address Information"
    /// </example>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional description for this subsection.
    /// </summary>
    /// <remarks>
    /// Provides additional context or instructions for the subsection.
    /// </remarks>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the list of prompts in this subsection.
    /// </summary>
    /// <remarks>
    /// Prompts are displayed in the order they appear in this list.
    /// </remarks>
    public List<Prompt> Prompts { get; set; } = new();
}
