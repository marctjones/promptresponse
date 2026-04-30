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
    /// Loads an APR document directly from a known file path. No dialog is shown.
    /// Used for command-line "--open path" startup and demo flows.
    /// </summary>
    /// <returns>The loaded document, or null if the path doesn't exist or fails to parse.</returns>
    Task<AprDocument?> LoadFileAsync(string filePath);

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

    /// <summary>
    /// Clears the current file path (used when opening a template for filling).
    /// </summary>
    void ClearCurrentFilePath();

    /// <summary>
    /// Sets the current file path (used when opening a file from command line).
    /// </summary>
    /// <param name="filePath">The file path to set.</param>
    void SetCurrentFilePath(string filePath);
}
