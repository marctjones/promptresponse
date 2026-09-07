using PromptResponse.Core.Models;
using PromptResponse.Core.Beta6;
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
        var document = await LoadSelectingBeta6OccurrenceAsync(path);
        if (document is null) return;
        _fileService.ClearCurrentFilePath();
        _session.Set(document, null, dirty: false);
    }

    public async Task OpenRecentAsync(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        var document = await LoadSelectingBeta6OccurrenceAsync(path);
        if (document is null) return;
        _fileService.SetCurrentFilePath(path);
        _session.Set(document, path, dirty: false);
        _addToRecent(path, document.Metadata.Title);
    }

    public async Task OpenAsync()
    {
        var document = await _fileService.OpenFileAsync();
        if (document is null) return;
        document = await SelectBeta6OccurrenceAsync(_fileService.CurrentFilePath, document);
        if (document is null) return;
        _session.Set(document, _fileService.CurrentFilePath, dirty: false);
        _addToRecent(_fileService.CurrentFilePath, document.Metadata.Title);
    }

    public async Task<bool> OpenFromPathAsync(string path)
    {
        var document = await LoadSelectingBeta6OccurrenceAsync(path);
        if (document is null) return false;
        _session.Set(document, path, dirty: false);
        _addToRecent(path, document.Metadata.Title);
        return true;
    }

    /// <summary>
    /// Makes stream occurrence selection a presentation decision. Attestations remain
    /// informational: a form is always editable after selection, regardless of whether
    /// an attestation is valid, unresolved, invalid, or unverifiable.
    /// </summary>
    private async Task<AprDocument?> LoadSelectingBeta6OccurrenceAsync(string path)
    {
        var document = await _fileService.LoadFileAsync(path);
        return document is null ? null : await SelectBeta6OccurrenceAsync(path, document);
    }

    private async Task<AprDocument?> SelectBeta6OccurrenceAsync(string? path, AprDocument fallback)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return fallback;
        var source = await File.ReadAllTextAsync(path);
        if (!source.Contains("1.0-beta.6", StringComparison.Ordinal)) return fallback;

        var representation = Path.GetExtension(path).Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
                             Path.GetExtension(path).Equals(".yml", StringComparison.OrdinalIgnoreCase)
            ? AprRepresentation.Yaml : AprRepresentation.Jsonc;
        var records = new AprBeta6Reader().ReadStream(source, representation);
        var forms = records.OfType<AprFormRecord>().ToList();
        if (forms.Count == 0) return null;

        var resolutions = AprAttestationResolver.Resolve(records);
        if (records.Count == 1) { _fileService.TrackBeta6FormSelection(path, 0); return forms[0].Form; }

        var summary = string.Join(Environment.NewLine, records.Select((record, index) => record switch
        {
            AprFormRecord form => $"{index + 1}. Form: {form.Form.Metadata.Title}",
            AprAttestationRecord => $"{index + 1}. Attestation: {resolutions[records.OfType<AprAttestationRecord>().TakeWhile(a => !ReferenceEquals(a, record)).Count()].State}",
            _ => $"{index + 1}. Unknown record"
        }));
        var choices = forms.Select((form, index) => $"Open form {index + 1}: {form.Form.Metadata.Title}").ToList();
        var chosen = await _dialogService.ShowChoiceAsync(
            "APR beta.6 stream",
            $"This stream contains independent records. Attestation status is informational and does not block editing.{Environment.NewLine}{Environment.NewLine}{summary}",
            choices);
        if (chosen is not >= 0 || chosen >= forms.Count) return null;
        _fileService.TrackBeta6FormSelection(path, chosen.Value);
        return forms[chosen.Value].Form;
    }

    /// <summary>Records that the content changed, which a save is not.</summary>
    /// <remarks>
    /// `metadata.modified` says when the document last changed. Stamping it on every
    /// save would say when the file was last written, and would move the digest of a
    /// document somebody only opened and closed — invalidating every attestation over
    /// it. The session is the one thing that knows the difference.
    /// </remarks>
    private void StampIfEdited()
    {
        if (_session.IsDirty && _session.CurrentDocument is { } document)
            document.Metadata.Modified = DateTime.UtcNow;
    }

    public async Task SaveAsync()
    {
        if (!_session.HasDocument) return;
        StampIfEdited();
        if (string.IsNullOrEmpty(_fileService.CurrentFilePath))
            await _fileService.SaveFileAsAsync(_session.CurrentDocument!, WarnAboutExtension);
        else
            await _fileService.SaveFileAsync(_session.CurrentDocument!, _fileService.CurrentFilePath);
        _session.MarkClean();
        _addToRecent(_fileService.CurrentFilePath, _session.CurrentDocument?.Metadata.Title);
    }

    public async Task SaveAsAsync()
    {
        if (!_session.HasDocument) return;
        StampIfEdited();
        await _fileService.SaveFileAsAsync(_session.CurrentDocument!, WarnAboutExtension);
        _session.MarkClean();
        _addToRecent(_fileService.CurrentFilePath, _session.CurrentDocument?.Metadata.Title);
    }

    /// <summary>Says that the chosen extension disagrees, and changes nothing either way.</summary>
    /// <remarks>
    /// APR-SEC-006 asks an implementation to warn on a mismatch rather than silently
    /// honouring either one. Honouring the extension would rewrite the document
    /// (APR-SEC-007 forbids it); honouring `documentType` in silence would leave somebody
    /// with a file whose name says the opposite of what it holds. So the person is told,
    /// and decides.
    /// </remarks>
    private Task<bool> WarnAboutExtension(string path)
    {
        var kind = _session.CurrentDocument?.DocumentType == DocumentType.Template
            ? "a template" : "a filled form";
        return _dialogService.ShowConfirmationAsync(
            "The extension does not match this document",
            $"{Path.GetFileName(path)} names the other kind of APR file, and this document "
            + $"is {kind}. The name will not change what it is — `documentType` decides "
            + $"that. Save it under this name anyway?");
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
