using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using System.Linq;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Implementation of file service using Avalonia file dialogs.
/// </summary>
public class FileService : IFileService
{
    private readonly IAprSerializer _serializer;
    private string? _currentFilePath;

    public FileService(IAprSerializer serializer)
    {
        _serializer = serializer;
    }

    public string? CurrentFilePath => _currentFilePath;

    public async Task<AprDocument?> OpenFileAsync()
    {
        // Get the main window
        var window = GetMainWindow();
        if (window == null) return null;

        // Show open file dialog
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open APR File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("APR Files") { Patterns = new[] { "*.apr" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (files.Count == 0)
        {
            return null;
        }

        var file = files[0];
        _currentFilePath = file.Path.LocalPath;

        // Load and deserialize
        await using var stream = await file.OpenReadAsync();
        var document = await _serializer.DeserializeAsync(stream);

        return document;
    }

    public async Task<bool> SaveFileAsAsync(AprDocument document)
    {
        var window = GetMainWindow();
        if (window == null) return false;

        // Show save file dialog
        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save APR File",
            DefaultExtension = "apr",
            SuggestedFileName = string.IsNullOrWhiteSpace(document.Metadata.Title)
                ? "document.apr"
                : MakeValidFileName(document.Metadata.Title) + ".apr",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("APR Files") { Patterns = new[] { "*.apr" } }
            }
        });

        if (file == null)
        {
            return false;
        }

        _currentFilePath = file.Path.LocalPath;
        await SaveFileAsync(document, _currentFilePath);

        return true;
    }

    public async Task SaveFileAsync(AprDocument document, string filePath)
    {
        // Update modified timestamp
        document.Metadata.Modified = DateTime.UtcNow;

        // Serialize and save
        await using var stream = File.Create(filePath);
        await _serializer.SerializeAsync(document, stream);

        _currentFilePath = filePath;
    }

    private static Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }

    private static string MakeValidFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
