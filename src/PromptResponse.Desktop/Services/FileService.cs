using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Beta6;
using System.Security.Cryptography.X509Certificates;
using System.Linq;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Implementation of file service using Avalonia file dialogs.
/// </summary>
public class FileService : IFileService
{
    private readonly IAprSerializer _serializer;
    private readonly AprDocumentPersistence _persistence;
    private readonly Dictionary<string, (IReadOnlyList<AprStreamRecord> Records, int FormRecordIndex)> _openStreams = new(StringComparer.Ordinal);
    private string? _currentFilePath;

    public FileService(IAprSerializer serializer)
    {
        _serializer = serializer;
        _persistence = new AprDocumentPersistence();
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
        var records = await _persistence.LoadStreamAsync(filePath);
        var first = records.Select((record, index) => (record, index)).FirstOrDefault(item => item.record is AprFormRecord);
        if (first.record is AprFormRecord)
            _openStreams[filePath] = (records, first.index);
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
                new FilePickerFileType("APR Files") { Patterns = new[] { "*.apr", "*.aprt", "*.aprf", "*.yaml", "*.yml" } },
                new FilePickerFileType("APR Templates") { Patterns = new[] { "*.aprt" } },
                new FilePickerFileType("APR Filled Forms") { Patterns = new[] { "*.aprf" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (files.Count == 0)
        {
            return null;
        }

        return await LoadFileAsync(files[0].Path.LocalPath);
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

        if (_openStreams.TryGetValue(filePath, out var stream))
        {
            var records = stream.Records.ToList();
            records[stream.FormRecordIndex] = new AprFormRecord(document, default);
            await _persistence.SaveStreamAsync(records, filePath);
            _openStreams[filePath] = (records, stream.FormRecordIndex);
        }
        else
        {
            await _persistence.SaveAsync(document, filePath);
            var records = await _persistence.LoadStreamAsync(filePath);
            var formIndex = records.Select((record, index) => (record, index)).First(item => item.record is AprFormRecord).index;
            _openStreams[filePath] = (records, formIndex);
        }

        _currentFilePath = filePath;
    }

    public void TrackBeta6FormSelection(string filePath, int formIndex)
    {
        if (!_openStreams.TryGetValue(filePath, out var stream)) return;
        var recordIndexes = stream.Records.Select((record, index) => (record, index))
            .Where(item => item.record is AprFormRecord).Select(item => item.index).ToList();
        if (formIndex >= 0 && formIndex < recordIndexes.Count)
            _openStreams[filePath] = (stream.Records, recordIndexes[formIndex]);
    }

    public async Task<bool> AppendBeta6AttestationAsync(AprDocument document, X509Certificate2 certificate, IReadOnlyList<string>? fields = null)
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath) || !_openStreams.TryGetValue(_currentFilePath, out var stream)) return false;
        var reader = new AprBeta6Reader();
        var subject = reader.ReadForm(reader.WriteForm(document, AprRepresentation.Jsonc), AprRepresentation.Jsonc);
        var value = reader.ReadStream(reader.WriteForm(subject, AprRepresentation.Jsonc), AprRepresentation.Jsonc).OfType<AprFormRecord>().Single().Value;
        var records = stream.Records.ToList();
        records[stream.FormRecordIndex] = new AprFormRecord(document, value);
        records.Add(AprAttestationFactory.Create(value, certificate, fields));
        await _persistence.SaveStreamAsync(records, _currentFilePath);
        _openStreams[_currentFilePath] = (records, stream.FormRecordIndex);
        return true;
    }

    public IReadOnlyList<AprAttestationResolution> GetBeta6Attestations() =>
        !string.IsNullOrWhiteSpace(_currentFilePath) && _openStreams.TryGetValue(_currentFilePath, out var stream)
            ? AprAttestationResolver.Resolve(stream.Records)
            : [];

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
