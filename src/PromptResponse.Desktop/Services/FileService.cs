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
    private readonly AprDocumentPersistence _persistence;
    private string? _currentFilePath;

    public FileService(IAprSerializer serializer)
    {
        _serializer = serializer;
        _persistence = new AprDocumentPersistence(serializer);
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
        var document = await _persistence.LoadAsync(filePath);
        if (document == null) return null;

        // documentType in the file is authoritative; the extension is a desktop
        // affordance only (specification section 5). The old rule — extension wins —
        // cannot be implemented anywhere a filename does not exist: an HTTP body, a
        // database column, a clipboard paste, a share intent. Under it a browser reader
        // and this app would reach different conclusions about identical bytes.
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

        // documentType in the file is authoritative (specification section 5). The
        // extension drives icons, file associations, and save dialogs — never meaning.

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

    public Task<string?> PickPdfExportPathAsync(string suggestedFileName) =>
        PickExportPathAsync(suggestedFileName, "Export PDF", "PDF Document", "pdf");

    public async Task<string?> PickPdfImportPathAsync()
    {
        var window = GetMainWindow();
        if (window == null) return null;

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import from PDF",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("PDF Document") { Patterns = new[] { "*.pdf" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        return files.Count == 0 ? null : files[0].Path.LocalPath;
    }

    public async Task<string?> PickCertificateAsync()
    {
        var window = GetMainWindow();
        if (window == null) return null;

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select signing certificate",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("PKCS#12 certificate") { Patterns = new[] { "*.pfx", "*.p12" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        return files.Count == 0 ? null : files[0].Path.LocalPath;
    }

    public async Task<string?> PickExportPathAsync(string suggestedFileName, string title, string typeLabel, string extension)
    {
        var window = GetMainWindow();
        if (window == null) return null;

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            DefaultExtension = extension,
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = new[]
            {
                new FilePickerFileType(typeLabel) { Patterns = new[] { "*." + extension } }
            }
        });

        return file?.Path.LocalPath;
    }

    public async Task SaveFileAsync(AprDocument document, string filePath)
    {
        // The document's own documentType is preserved on save (specification section 5).
        // Choosing a filename is naming a file, not redefining what the document is —
        // converting a template into a filled form is an explicit act elsewhere, which
        // sets documentType and records templateId.

        await _persistence.SaveAsync(document, filePath);

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
