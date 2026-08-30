using PromptResponse.Core.Rendering;
using PromptResponse.Desktop.Services;
using PromptResponse.Rendering.Pdf;

namespace PromptResponse.Desktop.ViewModels.Workflows;

/// <summary>
/// Coordinates output derived from the current document. It owns no shell state:
/// cancellation simply leaves the active document and its path unchanged.
/// </summary>
internal sealed class DocumentOutputWorkflow
{
    private readonly IDocumentSessionService _session;
    private readonly IFileService _fileService;
    private readonly IDialogService _dialogService;

    public DocumentOutputWorkflow(
        IDocumentSessionService session,
        IFileService fileService,
        IDialogService dialogService)
    {
        _session = session;
        _fileService = fileService;
        _dialogService = dialogService;
    }

    public async Task ShowPrintPreviewAsync()
    {
        var document = _session.CurrentDocument;
        if (document is null) return;

        var model = new DocumentRenderModelBuilder().Build(document, RenderOptions.Default);
        await _dialogService.ShowPrintPreviewAsync(model, includeEmptyFields: true);
    }

    public async Task ExportPdfAsync(bool fillable)
    {
        var document = _session.CurrentDocument;
        if (document is null) return;

        var suggested = SuggestedName(document.Metadata.Title, fillable ? "-form.pdf" : ".pdf");
        var path = await _fileService.PickPdfExportPathAsync(suggested);
        if (string.IsNullOrEmpty(path)) return;

        IDocumentRenderer renderer = fillable ? new FillablePdfDocumentRenderer() : new PdfDocumentRenderer();
        await using var stream = File.Create(path);
        renderer.Render(document, RenderOptions.Default, stream);
    }

    public async Task ExportHtmlAsync(bool fillable)
    {
        var document = _session.CurrentDocument;
        if (document is null) return;

        var suggested = SuggestedName(document.Metadata.Title, fillable ? "-form.html" : ".html");
        var title = fillable ? "Export web form" : "Export HTML";
        var path = await _fileService.PickExportPathAsync(suggested, title, "HTML Page", "html");
        if (string.IsNullOrEmpty(path)) return;

        IDocumentRenderer renderer = fillable ? new FillableHtmlDocumentRenderer() : new HtmlDocumentRenderer();
        await using var stream = File.Create(path);
        renderer.Render(document, RenderOptions.Default, stream);
    }

    private static string SuggestedName(string? title, string suffix)
    {
        var source = string.IsNullOrWhiteSpace(title) ? "form" : title;
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(source.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return (string.IsNullOrEmpty(cleaned) ? "form" : cleaned) + suffix;
    }
}
