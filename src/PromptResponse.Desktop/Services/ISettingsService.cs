using PromptResponse.Desktop.Models;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Service for loading and saving application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the current application settings.
    /// </summary>
    AppSettings Settings { get; }

    /// <summary>
    /// Loads settings from disk synchronously. If no settings file exists, uses default settings.
    /// </summary>
    void Load();

    /// <summary>
    /// Loads settings from disk asynchronously. If no settings file exists, uses default settings.
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// Saves current settings to disk synchronously.
    /// </summary>
    void Save();

    /// <summary>
    /// Saves current settings to disk asynchronously.
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// Adds a file to the recent files list.
    /// </summary>
    /// <param name="filePath">The file path to add.</param>
    void AddRecentFile(string filePath);

    /// <summary>
    /// Clears the recent files list.
    /// </summary>
    void ClearRecentFiles();
}
