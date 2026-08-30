using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using PromptResponse.Rendering.Pdf;
using PromptResponse.Desktop.Services;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>Export, preview, and PDF-import journeys for the desktop shell.</summary>
public partial class MainShellViewModelTests
{
    [Fact]
    public async Task ExportPdf_WritesPdfContainingCurrentValues()
    {
        var fs = Substitute.For<IFileService>(); var outPath = Path.Combine(Path.GetTempPath(), $"vm_export_{Guid.NewGuid():N}.pdf");
        fs.PickPdfExportPathAsync(Arg.Any<string>()).Returns(outPath); var session = new DocumentSessionService(); var doc = MakeTemplate(); doc.Sections[0].Prompts[0].Response = "Zaphod Beeblebrox"; session.Set(doc, null); var shell = CreateShell(fs, session: session);
        try { await shell.ExportPdf(); File.Exists(outPath).Should().BeTrue(); var bytes = await File.ReadAllBytesAsync(outPath); System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-"); using var pdf = Excise.Core.Document.PdfDocument.Open(bytes); string.Concat(Enumerable.Range(1, pdf.Pages.Count).Select(i => pdf.GetPage(i).Text)).Should().Contain("Zaphod Beeblebrox", "the export must capture the form's current values"); }
        finally { if (File.Exists(outPath)) File.Delete(outPath); }
    }

    [Fact]
    public async Task ExportPdf_WhenCancelled_WritesNothing()
    {
        var fs = Substitute.For<IFileService>(); fs.PickPdfExportPathAsync(Arg.Any<string>()).Returns((string?)null); var session = new DocumentSessionService(); session.Set(MakeTemplate(), null); var shell = CreateShell(fs, session: session);
        await shell.ExportPdf(); await fs.Received(1).PickPdfExportPathAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task PrintPreview_ShowsCurrentDocumentRenderModel()
    {
        var dialog = Substitute.For<IDialogService>(); var session = new DocumentSessionService(); var doc = MakeTemplate(); doc.Metadata.Description = "Preview description"; doc.Sections[0].Prompts[0].Response = "Zaphod Beeblebrox"; session.Set(doc, null); var shell = CreateShell(dialogService: dialog, session: session);
        await shell.PrintPreview(); await dialog.Received(1).ShowPrintPreviewAsync(Arg.Is<RenderModel>(m => m.Title == "Test Template" && m.Description == "Preview description" && m.Blocks.OfType<FieldBlock>().Any(f => f.Label == "Name" && f.Value == "Zaphod Beeblebrox")), includeEmptyFields: true);
    }

    [Fact]
    public async Task ExportPdfForm_WritesFillableAcroForm()
    {
        var fs = Substitute.For<IFileService>(); var outPath = Path.Combine(Path.GetTempPath(), $"vm_form_{Guid.NewGuid():N}.pdf"); fs.PickPdfExportPathAsync(Arg.Any<string>()).Returns(outPath); var session = new DocumentSessionService(); session.Set(MakeTemplate(), null); var shell = CreateShell(fs, session: session);
        try { await shell.ExportPdfForm(); using var pdf = Excise.Core.Document.PdfDocument.Open(await File.ReadAllBytesAsync(outPath)); var form = pdf.GetAcroForm(); form.Should().NotBeNull(); form!.Fields.Should().NotBeEmpty(); } finally { if (File.Exists(outPath)) File.Delete(outPath); }
    }

    [Fact]
    public async Task ExportHtml_WritesPageContainingCurrentValues()
    {
        var fs = Substitute.For<IFileService>(); var outPath = Path.Combine(Path.GetTempPath(), $"vm_export_{Guid.NewGuid():N}.html"); fs.PickExportPathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(outPath); var session = new DocumentSessionService(); var doc = MakeTemplate(); doc.Sections[0].Prompts[0].Response = "Zaphod Beeblebrox"; session.Set(doc, null); var shell = CreateShell(fs, session: session);
        try { await shell.ExportHtml(); File.Exists(outPath).Should().BeTrue(); var html = await File.ReadAllTextAsync(outPath); html.Should().StartWith("<!DOCTYPE html>").And.Contain("Zaphod Beeblebrox", "the export must capture the form's current values"); } finally { if (File.Exists(outPath)) File.Delete(outPath); }
    }

    [Fact]
    public async Task ExportHtmlForm_WritesInteractiveForm()
    {
        var fs = Substitute.For<IFileService>(); var outPath = Path.Combine(Path.GetTempPath(), $"vm_form_{Guid.NewGuid():N}.html"); fs.PickExportPathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(outPath); var session = new DocumentSessionService(); session.Set(MakeTemplate(), null); var shell = CreateShell(fs, session: session);
        try { await shell.ExportHtmlForm(); var html = await File.ReadAllTextAsync(outPath); html.Should().Contain("<form id=\"apr-form\"").And.Contain("id=\"apr-download\"").And.Contain(".aprf"); } finally { if (File.Exists(outPath)) File.Delete(outPath); }
    }

    [Fact]
    public async Task ExportHtml_WhenCancelled_WritesNothing()
    {
        var fs = Substitute.For<IFileService>(); fs.PickExportPathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns((string?)null); var session = new DocumentSessionService(); session.Set(MakeTemplate(), null); var shell = CreateShell(fs, session: session);
        await shell.ExportHtml(); await fs.Received(1).PickExportPathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ImportPdf_FillablePdf_LoadsTemplateIntoSessionAsDirty()
    {
        var fs = Substitute.For<IFileService>(); var pdf = Path.Combine(Path.GetTempPath(), $"imp_{Guid.NewGuid():N}.pdf"); await File.WriteAllBytesAsync(pdf, new FillablePdfDocumentRenderer().RenderToBytes(MakeTemplate())); fs.PickPdfImportPathAsync().Returns(pdf); var session = new DocumentSessionService(); var shell = CreateShell(fs, session: session);
        try { await shell.ImportPdf(); session.CurrentDocument.Should().NotBeNull("the imported PDF should become the open document"); session.CurrentDocument!.DocumentType.Should().Be(DocumentType.Template); session.IsDirty.Should().BeTrue("an import is unsaved until the user picks a path"); } finally { if (File.Exists(pdf)) File.Delete(pdf); }
    }

    [Fact]
    public async Task ImportPdf_WhenCancelled_NoOp()
    {
        var fs = Substitute.For<IFileService>(); fs.PickPdfImportPathAsync().Returns((string?)null); var session = new DocumentSessionService(); var shell = CreateShell(fs, session: session);
        await shell.ImportPdf(); session.CurrentDocument.Should().BeNull(); await fs.Received(1).PickPdfImportPathAsync();
    }

    [Fact]
    public async Task ImportPdf_FlatPdf_ShowsDialog_AndDoesNotOpen()
    {
        var fs = Substitute.For<IFileService>(); var dlg = Substitute.For<IDialogService>(); var pdf = Path.Combine(Path.GetTempPath(), $"flat_{Guid.NewGuid():N}.pdf"); await File.WriteAllBytesAsync(pdf, new PdfDocumentRenderer().RenderToBytes(MakeTemplate())); fs.PickPdfImportPathAsync().Returns(pdf); var session = new DocumentSessionService(); var shell = CreateShell(fs, dialogService: dlg, session: session);
        try { await shell.ImportPdf(); session.CurrentDocument.Should().BeNull("a flat PDF has no fields to import"); await dlg.Received(1).ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>()); } finally { if (File.Exists(pdf)) File.Delete(pdf); }
    }

    private static byte[] CrypticFillablePdf() => new FillablePdfDocumentRenderer().RenderToBytes(new AprDocument { DocumentType = DocumentType.Template, Metadata = new Metadata { Title = "Raw" }, Sections = [new Section { Id = "p1", Title = "Page 1", Prompts = [new Prompt { Id = "f1_1", Label = "f1_1[0]", Hints = new PromptHints { ExpectedDataType = "text" } }, new Prompt { Id = "f1_2", Label = "f1_2[0]", Hints = new PromptHints { ExpectedDataType = "text" } }] }] });

    [Fact]
    public async Task ImportPdf_LowQuality_Declined_DoesNotOpen()
    {
        var fs = Substitute.For<IFileService>(); var dlg = Substitute.For<IDialogService>(); var pdf = Path.Combine(Path.GetTempPath(), $"raw_{Guid.NewGuid():N}.pdf"); await File.WriteAllBytesAsync(pdf, CrypticFillablePdf()); fs.PickPdfImportPathAsync().Returns(pdf); dlg.ShowImportReviewAsync(Arg.Any<ImportQuality>()).Returns(false); var session = new DocumentSessionService(); var shell = CreateShell(fs, dialogService: dlg, session: session);
        try { await shell.ImportPdf(); await dlg.Received(1).ShowImportReviewAsync(Arg.Is<ImportQuality>(q => q.Recommendation == ImportRecommendation.UseSkillInstead && q.Flags.Any(f => f.Kind == FieldFlagKind.CrypticLabel))); session.CurrentDocument.Should().BeNull("declining the low-quality warning must not open the import"); } finally { if (File.Exists(pdf)) File.Delete(pdf); }
    }

    [Fact]
    public async Task ImportPdf_LowQuality_Accepted_Opens()
    {
        var fs = Substitute.For<IFileService>(); var dlg = Substitute.For<IDialogService>(); var pdf = Path.Combine(Path.GetTempPath(), $"raw_{Guid.NewGuid():N}.pdf"); await File.WriteAllBytesAsync(pdf, CrypticFillablePdf()); fs.PickPdfImportPathAsync().Returns(pdf); dlg.ShowImportReviewAsync(Arg.Any<ImportQuality>()).Returns(true); var session = new DocumentSessionService(); var shell = CreateShell(fs, dialogService: dlg, session: session);
        try { await shell.ImportPdf(); session.CurrentDocument.Should().NotBeNull(); session.IsDirty.Should().BeTrue(); await dlg.Received(1).ShowImportReviewAsync(Arg.Is<ImportQuality>(q => q.Recommendation == ImportRecommendation.UseSkillInstead && q.Flags.Any(f => f.Kind == FieldFlagKind.CrypticLabel))); } finally { if (File.Exists(pdf)) File.Delete(pdf); }
    }
}
