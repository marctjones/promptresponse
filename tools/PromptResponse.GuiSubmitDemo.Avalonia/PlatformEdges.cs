using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Rendering.Pdf;

namespace PromptResponse.GuiSubmitDemo.Avalonia;

/// <summary>A file service that touches no filesystem and picks nothing.</summary>
/// <remarks>
/// The HTTPS submission path (<c>DocumentDeliveryWorkflow.SubmitViaHttpsAsync</c>) never
/// calls <see cref="IFileService"/> at all — it reads <c>Metadata.SubmissionUrls</c> and
/// serializes the in-session document directly. This exists only because
/// <see cref="MainShellViewModel"/>'s constructor requires one; the document under test
/// is loaded straight into the session (see <see cref="Program"/>), not through a picker.
/// </remarks>
internal sealed class NoFiles : IFileService
{
    public string? CurrentFilePath => null;
    public void ClearCurrentFilePath() { }
    public void SetCurrentFilePath(string filePath) { }
    public Task<AprDocument?> OpenFileAsync() => Task.FromResult<AprDocument?>(null);
    public Task<AprDocument?> LoadFileAsync(string filePath) => Task.FromResult<AprDocument?>(null);
    public Task<bool> SaveFileAsAsync(AprDocument document,
        Func<string, Task<bool>>? confirmExtensionMismatch = null) => Task.FromResult(false);
    public Task SaveFileAsync(AprDocument document, string filePath) => Task.CompletedTask;
    public Task<string?> PickPdfExportPathAsync(string suggestedFileName) => Task.FromResult<string?>(null);
    public Task<string?> PickExportPathAsync(string suggestedFileName, string title,
        string typeLabel, string extension) => Task.FromResult<string?>(null);
    public Task<string?> PickPdfImportPathAsync() => Task.FromResult<string?>(null);
    public Task<string?> PickCertificateAsync() => Task.FromResult<string?>(null);
}

/// <summary>
/// A dialog service that answers exactly what the real "Submit via HTTPS" flow needs to
/// proceed, printing each dialog's title and message to the console first.
/// </summary>
/// <remarks>
/// A demo whose point is showing a person what the real app asks before it sends anything
/// must not swallow those questions silently — it prints them, then answers as a person
/// confirming would, the same role <c>--yes</c> plays for the CLI's own submit command.
/// </remarks>
internal sealed class AutoConfirmDialogs : IDialogService
{
    public Task<bool> ShowConfirmationAsync(string title, string message)
    {
        Console.WriteLine($"  [dialog] {title}: {message}");
        Console.WriteLine("  [dialog] -> confirmed");
        return Task.FromResult(true);
    }

    public Task<string?> ShowInputAsync(string title, string message, string defaultValue = "",
        bool isPassword = false) => Task.FromResult<string?>(defaultValue);

    public Task<int?> ShowChoiceAsync(string title, string message, IReadOnlyList<string> choices)
    {
        Console.WriteLine($"  [dialog] {title}: {message}");
        for (var i = 0; i < choices.Count; i++) Console.WriteLine($"    {i}. {choices[i]}");
        Console.WriteLine($"  [dialog] -> chose 0 ({choices[0]})");
        return Task.FromResult<int?>(0);
    }

    public Task ShowPrintPreviewAsync(RenderModel model, bool includeEmptyFields) => Task.CompletedTask;
    public Task<bool> ShowImportReviewAsync(ImportQuality quality) => Task.FromResult(false);
}
