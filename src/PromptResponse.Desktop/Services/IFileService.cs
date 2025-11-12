using PromptResponse.Core.Models;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Service for file operations.
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Shows an open file dialog and loads an APR document.
    /// </summary>
    /// <returns>The loaded document, or null if cancelled.</returns>
    Task<AprDocument?> OpenFileAsync();

    /// <summary>
    /// Shows a save file dialog and saves an APR document.
    /// </summary>
    /// <param name="document">The document to save.</param>
    /// <returns>True if saved successfully.</returns>
    Task<bool> SaveFileAsAsync(AprDocument document);

    /// <summary>
    /// Saves an APR document to a specific path.
    /// </summary>
    /// <param name="document">The document to save.</param>
    /// <param name="filePath">The file path.</param>
    Task SaveFileAsync(AprDocument document, string filePath);

    /// <summary>
    /// Gets the last opened or saved file path.
    /// </summary>
    string? CurrentFilePath { get; }
}
