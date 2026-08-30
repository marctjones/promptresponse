using PromptResponse.Core.Models;
using PromptResponse.Desktop.Services;
using PromptResponse.Rendering.Pdf;

namespace PromptResponse.Desktop.ViewModels.Workflows;

/// <summary>
/// Coordinates document session transitions and their file-service effects.
/// Presentation policy remains with the shell; this workflow owns no view state.
/// </summary>
internal sealed class DocumentSessionWorkflow
{
    private readonly IDocumentSessionService _session;
    private readonly IFileService _fileService;
    private readonly IDialogService _dialogService;
    private readonly Action<string?, string?> _addToRecent;

    public DocumentSessionWorkflow(IDocumentSessionService session, IFileService fileService,
        IDialogService dialogService, Action<string?, string?> addToRecent)
    {
        _session = session;
        _fileService = fileService;
        _dialogService = dialogService;
        _addToRecent = addToRecent;
    }

    public async Task NewFromTemplateAsync(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        var document = await _fileService.LoadFileAsync(path);
        if (document is null) return;
        _fileService.ClearCurrentFilePath();
        _session.Set(document, null, dirty: false);
    }

    public async Task OpenRecentAsync(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        var document = await _fileService.LoadFileAsync(path);
        if (document is null) return;
        _fileService.SetCurrentFilePath(path);
        _session.Set(document, path, dirty: false);
        _addToRecent(path, document.Metadata.Title);
    }

    public async Task OpenAsync()
    {
        var document = await _fileService.OpenFileAsync();
        if (document is null) return;
        _session.Set(document, _fileService.CurrentFilePath, dirty: false);
        _addToRecent(_fileService.CurrentFilePath, document.Metadata.Title);
    }

    public async Task<bool> OpenFromPathAsync(string path)
    {
        var document = await _fileService.LoadFileAsync(path);
        if (document is null) return false;
        _session.Set(document, path, dirty: false);
        _addToRecent(path, document.Metadata.Title);
        return true;
    }

    public async Task SaveAsync()
    {
        if (!_session.HasDocument) return;
        if (string.IsNullOrEmpty(_fileService.CurrentFilePath))
            await _fileService.SaveFileAsAsync(_session.CurrentDocument!);
        else
            await _fileService.SaveFileAsync(_session.CurrentDocument!, _fileService.CurrentFilePath);
        _session.MarkClean();
        _addToRecent(_fileService.CurrentFilePath, _session.CurrentDocument?.Metadata.Title);
    }

    public async Task SaveAsAsync()
    {
        if (!_session.HasDocument) return;
        await _fileService.SaveFileAsAsync(_session.CurrentDocument!);
        _session.MarkClean();
        _addToRecent(_fileService.CurrentFilePath, _session.CurrentDocument?.Metadata.Title);
    }

    public async Task ImportPdfAsync()
    {
        var path = await _fileService.PickPdfImportPathAsync();
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            var (document, quality) = new PdfFormImporter().ImportWithQuality(path);
            if (quality.Recommendation != ImportRecommendation.UseDirectly
                && !await _dialogService.ShowImportReviewAsync(quality)) return;
            _session.Set(document, filePath: null, dirty: true);
        }
        catch (PdfFormImporter.NoFormFieldsException exception)
        {
            await _dialogService.ShowConfirmationAsync("Nothing to import", exception.Message);
        }
        catch (Exception exception)
        {
            await _dialogService.ShowConfirmationAsync("Import failed", $"Could not import the PDF: {exception.Message}");
        }
    }

    public async Task CloseAsync()
    {
        if (_session.IsDirty && !await _dialogService.ShowConfirmationAsync(
                "Unsaved changes", "You have unsaved changes. Close anyway?")) return;
        _session.Close();
        _fileService.ClearCurrentFilePath();
    }
}
