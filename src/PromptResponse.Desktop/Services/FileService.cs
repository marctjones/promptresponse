using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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

    public void ClearCurrentFilePath()
    {
        _currentFilePath = null;
    }

    public void SetCurrentFilePath(string filePath)
    {
        _currentFilePath = filePath;
    }

    public async Task<AprDocument?> LoadFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;

        await using var stream = File.OpenRead(filePath);
        var document = await _serializer.DeserializeAsync(stream);
        if (document == null) return null;

        // Extension-based DocumentType override (extension takes precedence over content).
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension == ".aprt") document.DocumentType = DocumentType.Template;
        else if (extension == ".aprf") document.DocumentType = DocumentType.FilledForm;

        _currentFilePath = filePath;
        return document;
    }

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
                new FilePickerFileType("APR Files") { Patterns = new[] { "*.apr", "*.aprt", "*.aprf" } },
                new FilePickerFileType("APR Templates") { Patterns = new[] { "*.aprt" } },
                new FilePickerFileType("APR Filled Forms") { Patterns = new[] { "*.aprf" } },
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

        // Extension-based DocumentType override (extension takes precedence over content)
        if (document != null)
        {
            var extension = Path.GetExtension(_currentFilePath).ToLowerInvariant();
            if (extension == ".aprt")
            {
                // .aprt extension = always treat as template
                document.DocumentType = DocumentType.Template;
            }
            else if (extension == ".aprf")
            {
                // .aprf extension = always treat as filled form
                document.DocumentType = DocumentType.FilledForm;
            }
            // .apr extension = use DocumentType from file content (no override)
        }

        return document;
    }

    public async Task<bool> SaveFileAsAsync(AprDocument document)
    {
        var window = GetMainWindow();
        if (window == null) return false;

        // Determine suggested extension based on DocumentType
        var extension = document.DocumentType == DocumentType.Template ? ".aprt" : ".aprf";
        var defaultExtension = extension.TrimStart('.');

        // Show save file dialog
        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save APR File",
            DefaultExtension = defaultExtension,
            SuggestedFileName = string.IsNullOrWhiteSpace(document.Metadata.Title)
                ? $"document{extension}"
                : MakeValidFileName(document.Metadata.Title) + extension,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("APR Template") { Patterns = new[] { "*.aprt" } },
                new FilePickerFileType("APR Filled Form") { Patterns = new[] { "*.aprf" } },
                new FilePickerFileType("APR Generic") { Patterns = new[] { "*.apr" } }
            }
        });

        if (file == null)
        {
            return false;
        }

        _currentFilePath = file.Path.LocalPath;

        // Update DocumentType based on chosen extension (extension determines type)
        var chosenExtension = Path.GetExtension(_currentFilePath).ToLowerInvariant();
        if (chosenExtension == ".aprt")
        {
            document.DocumentType = DocumentType.Template;
        }
        else if (chosenExtension == ".aprf")
        {
            document.DocumentType = DocumentType.FilledForm;
        }
        // .apr keeps current DocumentType

        await SaveFileAsync(document, _currentFilePath);

        return true;
    }

    public async Task SaveFileAsync(AprDocument document, string filePath)
    {
        // Update DocumentType based on file extension (extension determines type)
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension == ".aprt")
        {
            document.DocumentType = DocumentType.Template;
        }
        else if (extension == ".aprf")
        {
            document.DocumentType = DocumentType.FilledForm;
        }
        // .apr keeps current DocumentType

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
