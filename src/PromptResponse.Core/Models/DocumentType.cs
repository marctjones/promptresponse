namespace PromptResponse.Core.Models;

/// <summary>
/// Specifies the type of APR document.
/// </summary>
public enum DocumentType
{
    /// <summary>
    /// A blank template to be filled out.
    /// </summary>
    /// <remarks>
    /// Templates define the structure and prompts but typically have empty responses.
    /// When a user opens a template, they can choose to edit the template structure
    /// or fill it out as a form.
    /// </remarks>
    Template = 0,

    /// <summary>
    /// A form that has been filled out with responses.
    /// </summary>
    /// <remarks>
    /// Filled forms are based on templates and contain user-provided responses.
    /// When opening a filled form, the application goes directly to editing mode.
    /// </remarks>
    FilledForm = 1
}
