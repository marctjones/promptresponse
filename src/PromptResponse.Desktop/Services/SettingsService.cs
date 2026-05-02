using Microsoft.Extensions.Logging;
using PromptResponse.Desktop.Models;
using System.Text.Json;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Implementation of settings service that persists to JSON file.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsFilePath;
    private AppSettings _settings;

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;

        // Store settings in user's app data directory
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDirectory = Path.Combine(appDataPath, "PromptResponse");
        Directory.CreateDirectory(appDirectory);

        _settingsFilePath = Path.Combine(appDirectory, "settings.json");
        _settings = new AppSettings();

        _logger.LogInformation("SettingsService initialized. Settings path: {Path}", _settingsFilePath);
    }

    public AppSettings Settings => _settings;

    public void Load()
    {
        _logger.LogInformation("Loading settings from {Path}", _settingsFilePath);

        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);

                if (settings != null)
                {
                    _settings = settings;
                    _logger.LogInformation("Settings loaded successfully");
                    _logger.LogDebug("  Window Size: {Width}x{Height}", _settings.Window.Width, _settings.Window.Height);
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize settings, using defaults");
                    _settings = new AppSettings();
                }
            }
            else
            {
                _logger.LogInformation("No settings file found, using defaults");
                _settings = new AppSettings();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings, using defaults");
            _settings = new AppSettings();
        }
    }

    public void Save()
    {
        _logger.LogInformation("Saving settings to {Path}", _settingsFilePath);

        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_settings, options);
            File.WriteAllText(_settingsFilePath, json);
            _logger.LogInformation("Settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving settings");
        }
    }
}
