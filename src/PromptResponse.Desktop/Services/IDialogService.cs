namespace PromptResponse.Desktop.Services;

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
}
