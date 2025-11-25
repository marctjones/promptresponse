namespace PromptResponse.Desktop.Services;

/// <summary>
/// Service for displaying dialogs to the user.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows an error dialog with a title and message.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The error message to display.</param>
    /// <returns>A task that completes when the dialog is closed.</returns>
    Task ShowErrorAsync(string title, string message);

    /// <summary>
    /// Shows an information dialog with a title and message.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The information message to display.</param>
    /// <returns>A task that completes when the dialog is closed.</returns>
    Task ShowInfoAsync(string title, string message);

    /// <summary>
    /// Shows a warning dialog with a title and message.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The warning message to display.</param>
    /// <returns>A task that completes when the dialog is closed.</returns>
    Task ShowWarningAsync(string title, string message);

    /// <summary>
    /// Shows a confirmation dialog with Yes/No options.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The confirmation message to display.</param>
    /// <returns>True if the user clicked Yes, false otherwise.</returns>
    Task<bool> ShowConfirmationAsync(string title, string message);
}
