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
}
