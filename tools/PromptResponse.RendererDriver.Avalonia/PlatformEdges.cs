using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Rendering.Pdf;

namespace PromptResponse.RendererDriver.Avalonia;

/// <summary>A file service that touches no filesystem and picks nothing.</summary>
/// <remarks>
/// A renderer case asks what a person ended up in front of. Opening and saving are not
/// part of that question, and a driver has no picker to show, so these do nothing and say
/// so. `saveResult` is answered separately, by validation, which is where APR-RENDER-006
/// actually lives.
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

/// <summary>A dialog service that answers nothing, because nobody is there.</summary>
internal sealed class NoDialogs : IDialogService
{
    public Task<bool> ShowConfirmationAsync(string title, string message) => Task.FromResult(false);
    public Task<string?> ShowInputAsync(string title, string message, string defaultValue = "",
        bool isPassword = false) => Task.FromResult<string?>(null);
    public Task<int?> ShowChoiceAsync(string title, string message, IReadOnlyList<string> choices)
        => Task.FromResult<int?>(null);
    public Task ShowPrintPreviewAsync(RenderModel model, bool includeEmptyFields) => Task.CompletedTask;
    public Task<bool> ShowImportReviewAsync(ImportQuality quality) => Task.FromResult(false);
}
