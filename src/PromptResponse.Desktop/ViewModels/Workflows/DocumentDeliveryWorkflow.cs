using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Desktop.Services;

namespace PromptResponse.Desktop.ViewModels.Workflows;

/// <summary>
/// Coordinates deliberate delivery of a completed APR without changing the
/// active document or sending anything implicitly. File output, recipient
/// selection, confirmation, and transport are one user-facing workflow.
/// </summary>
internal sealed class DocumentDeliveryWorkflow
{
    private readonly IDocumentSessionService _session;
    private readonly IFileService _fileService;
    private readonly IDialogService _dialogService;
    private readonly IAprSerializer _serializer;
    private readonly IMailHandoffService _mailHandoff;
    private readonly IHttpsSubmissionService _httpsSubmission;
    private readonly Action<string?, string?> _addToRecent;

    public DocumentDeliveryWorkflow(
        IDocumentSessionService session,
        IFileService fileService,
        IDialogService dialogService,
        IAprSerializer serializer,
        IMailHandoffService mailHandoff,
        IHttpsSubmissionService httpsSubmission,
        Action<string?, string?> addToRecent)
    {
        _session = session;
        _fileService = fileService;
        _dialogService = dialogService;
        _serializer = serializer;
        _mailHandoff = mailHandoff;
        _httpsSubmission = httpsSubmission;
        _addToRecent = addToRecent;
    }

    public bool CanSubmitViaEmail(bool isEditMode) =>
        _session.HasDocument && !isEditMode &&
        _session.CurrentDocument?.Metadata.SubmissionUrls?.Any(IsEmailTarget) == true;

    public bool CanSubmitViaHttps(bool isEditMode) =>
        _session.HasDocument && !isEditMode &&
        _session.CurrentDocument?.Metadata.SubmissionUrls?.Any(IsHttpsTarget) == true;

    public async Task SubmitViaEmailAsync()
    {
        var source = _session.CurrentDocument;
        if (source?.Metadata.SubmissionUrls is not { Count: > 0 } targets) return;

        var choices = targets.Where(IsEmailTarget).ToList();
        if (choices.Count == 0) return;

        var selectedIndex = await _dialogService.ShowChoiceAsync(
            "Submit via email",
            "Choose the email destination. This opens a draft only; you review and send it yourself.",
            choices);
        if (!TryGetChoice(selectedIndex, choices, out var selected)) return;

        var outputPath = await _fileService.PickExportPathAsync(
            SuggestedSubmissionFileName(source.Metadata.Title), "Save completed APR file", "APR Filled Form", "aprf");
        if (string.IsNullOrWhiteSpace(outputPath)) return;

        var target = choices[selected];
        if (!await _dialogService.ShowConfirmationAsync(
                "Open email draft",
                $"A completed APR copy will be saved as {Path.GetFileName(outputPath)} and an email draft addressed to {target} will be opened. PromptResponse will not send the email. Continue?")) return;

        var completedCopy = CreateCompletedCopy(source);
        var previousPath = _fileService.CurrentFilePath;
        await _fileService.SaveFileAsync(completedCopy, outputPath);
        RestoreCurrentPath(previousPath);

        var result = await _mailHandoff.ComposeAsync(new MailHandoffRequest(
            target, outputPath,
            $"Completed APR form: {source.Metadata.Title ?? "Untitled form"}",
            "Please find the completed APR form attached."));
        _addToRecent(outputPath, completedCopy.Metadata.Title);
        await _dialogService.ShowConfirmationAsync("Email handoff", result.Message + Environment.NewLine + outputPath);
    }

    public async Task SubmitViaHttpsAsync()
    {
        var source = _session.CurrentDocument;
        var targets = source?.Metadata.SubmissionUrls?.Where(IsHttpsTarget).ToList() ?? [];
        if (targets.Count == 0) return;

        var selectedIndex = await _dialogService.ShowChoiceAsync(
            "Submit via HTTPS",
            "Choose one destination. PromptResponse will POST only after you confirm; it never follows redirects or falls back.",
            targets);
        if (!TryGetChoice(selectedIndex, targets, out var selected)) return;

        var target = targets[selected];
        if (!await _dialogService.ShowConfirmationAsync("Submit completed APR", $"POST this completed APR to {target}?")) return;

        var result = await _httpsSubmission.SubmitAsync(target, _serializer.Serialize(CreateCompletedCopy(source!)));
        await _dialogService.ShowConfirmationAsync("HTTPS submission", result.Message);
    }

    private AprDocument CreateCompletedCopy(AprDocument source)
    {
        var copy = _serializer.Deserialize(_serializer.Serialize(source));
        copy.DocumentType = DocumentType.FilledForm;
        return copy;
    }

    private void RestoreCurrentPath(string? previousPath)
    {
        if (string.IsNullOrEmpty(previousPath)) _fileService.ClearCurrentFilePath();
        else _fileService.SetCurrentFilePath(previousPath);
    }

    private static bool IsEmailTarget(string url) => MailHandoffService.TryGetRecipient(url, out _);

    private static bool IsHttpsTarget(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static bool TryGetChoice(int? choice, IReadOnlyList<string> choices, out int selected)
    {
        selected = choice.GetValueOrDefault();
        return choice.HasValue && selected >= 0 && selected < choices.Count;
    }

    private static string SuggestedSubmissionFileName(string? title) =>
        string.IsNullOrWhiteSpace(title) ? "completed-form.aprf" : $"{title}-completed.aprf";
}
