namespace PromptResponse.Desktop.Services;

using PromptResponse.Core.Rendering;
using PromptResponse.Rendering.Pdf;

/// <summary>
/// Service for displaying dialogs to the user.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a confirmation dialog with Yes/No options.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The confirmation message to display.</param>
    /// <returns>True if the user clicked Yes, false otherwise.</returns>
    Task<bool> ShowConfirmationAsync(string title, string message);

    /// <summary>
    /// Shows a single-line text input dialog and returns the entered text, or
    /// <c>null</c> if the user cancelled. An empty string means the user accepted
    /// with no text (distinct from cancelling).
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The prompt shown above the input.</param>
    /// <param name="defaultValue">The initial text in the input.</param>
    /// <param name="isPassword">Whether to mask the input (for passwords).</param>
    Task<string?> ShowInputAsync(string title, string message, string defaultValue = "", bool isPassword = false);

    /// <summary>
    /// Shows one explicit, keyboard- and screen-reader-accessible choice. Returns
    /// the zero-based selected index, or <c>null</c> when cancelled.
    /// </summary>
    Task<int?> ShowChoiceAsync(string title, string message, IReadOnlyList<string> choices);

    /// <summary>
    /// Shows an in-app print preview of the semantic render model that PDF export
    /// will consume. This is a preview of generated content/order, not an editor.
    /// </summary>
    /// <param name="model">The flattened document render model.</param>
    /// <param name="includeEmptyFields">Whether blank fields are included in the preview.</param>
    Task ShowPrintPreviewAsync(RenderModel model, bool includeEmptyFields);

    /// <summary>
    /// Shows a review dialog for a PDF import whose labels/types need attention.
    /// Returns true when the user chooses to open the imported template anyway.
    /// </summary>
    /// <param name="quality">The import quality assessment and field-review flags.</param>
    Task<bool> ShowImportReviewAsync(ImportQuality quality);
}
