namespace PromptResponse.Core.Models;

/// <summary>
/// Metadata for an APR document.
/// </summary>
/// <remarks>
/// Contains different fields depending on whether the document is a template or filled form.
/// Common fields: Title, Description, Created, Modified
/// Template-specific: Author, TemplateId, TemplateVersion
/// FilledForm-specific: TemplateId, TemplateVersion, FilledBy, FilledDate
/// </remarks>
public class Metadata
{
    /// <summary>
    /// Gets or sets the title of the document.
    /// </summary>
    /// <remarks>
    /// Required field. Describes what the form is for.
    /// </remarks>
    /// <example>
    /// "Employment Application Form", "Customer Survey"
    /// </example>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional description of the document.
    /// </summary>
    /// <remarks>
    /// Provides additional context about the form's purpose.
    /// </remarks>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the document was created.
    /// </summary>
    /// <remarks>
    /// Primarily used in templates. Should be UTC timestamp.
    /// </remarks>
    public DateTime? Created { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the document was last modified.
    /// </summary>
    /// <remarks>
    /// Used in both templates and filled forms. Should be UTC timestamp.
    /// Updated whenever the document is saved.
    /// </remarks>
    public DateTime? Modified { get; set; }

    /// <summary>
    /// Gets or sets the author of the template.
    /// </summary>
    /// <remarks>
    /// Template-specific field. Identifies who created the template.
    /// </remarks>
    /// <example>
    /// "HR Department", "John Doe"
    /// </example>
    public string? Author { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the template.
    /// </summary>
    /// <remarks>
    /// Used in templates to identify the template.
    /// Used in filled forms to reference which template was used.
    /// Should be stable across template versions.
    /// </remarks>
    /// <example>
    /// "employment-app", "customer-survey-q4-2025"
    /// </example>
    public string? TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the version of the template.
    /// </summary>
    /// <remarks>
    /// Tracks template versions. Filled forms should record which version they're based on.
    /// </remarks>
    /// <example>
    /// "1.0", "2.1", "2025-Q4"
    /// </example>
    public string? TemplateVersion { get; set; }

    /// <summary>
    /// Gets or sets the name of the person who filled out the form.
    /// </summary>
    /// <remarks>
    /// FilledForm-specific field. Optional identifier of who completed the form.
    /// </remarks>
    public string? FilledBy { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the form was initially filled out.
    /// </summary>
    /// <remarks>
    /// FilledForm-specific field. Distinct from Modified, which tracks any edits.
    /// Should be UTC timestamp.
    /// </remarks>
    public DateTime? FilledDate { get; set; }

    /// <summary>
    /// Gets or sets the digital signatures applied to the template.
    /// </summary>
    /// <remarks>
    /// Template-specific field. Contains signatures from template publishers (e.g., IRS, HR department)
    /// that establish the authenticity and integrity of the template structure.
    /// Multiple signatures can be present if co-signed or countersigned.
    /// </remarks>
    public List<DigitalSignature>? TemplateSignatures { get; set; }

    /// <summary>
    /// Gets or sets the digital signatures applied to the filled form.
    /// </summary>
    /// <remarks>
    /// FilledForm-specific field. Contains signatures from the person(s) who completed the form,
    /// establishing the authenticity and integrity of their responses.
    /// Multiple signatures can be present if the form requires multiple signatories.
    /// </remarks>
    public List<DigitalSignature>? FormSignatures { get; set; }
}
