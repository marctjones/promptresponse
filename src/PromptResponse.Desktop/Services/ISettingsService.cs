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
    /// Loads settings from disk. If no settings file exists, uses default settings.
    /// </summary>
    void Load();

    /// <summary>
    /// Saves current settings to disk.
    /// </summary>
    void Save();
}
