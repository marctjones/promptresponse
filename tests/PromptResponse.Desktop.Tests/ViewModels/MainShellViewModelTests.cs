using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using PromptResponse.Core.Serialization;
using PromptResponse.Rendering.Pdf;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// MainShellViewModel is the thin replacement for the legacy 879-line
/// MainWindowViewModel. It composes DocumentSessionService, ProfileService,
/// FormProgressViewModel, SearchViewModel, and PromptViewModelFactory rather than
/// owning them. CommandToolkit.Mvvm source generators handle [ObservableProperty]
/// and [RelayCommand].
/// </summary>
public class MainShellViewModelTests
{
    private sealed class StubProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static MainShellViewModel CreateShell(
        IFileService? fileService = null,
        IDialogService? dialogService = null,
        IDocumentSessionService? session = null,
        IProfileService? profile = null,
        IRecentFilesService? recentFiles = null,
        ITemplateCatalogService? templateCatalog = null)
    {
        fileService ??= Substitute.For<IFileService>();
        dialogService ??= Substitute.For<IDialogService>();
        session ??= new DocumentSessionService();
        profile ??= new ProfileService(new StubProbe(), applyAffordanceDefaults: false);
        var factory = new PromptViewModelFactory(profile);
        return new MainShellViewModel(fileService, dialogService, session, profile, factory,
            recentFiles: recentFiles, templateCatalog: templateCatalog);
    }

    private static AprDocument MakeTemplate() => new()
    {
        Version = "1.0",
        DocumentType = DocumentType.Template,
        Metadata = new Metadata { Title = "Test Template" },
        Sections = new List<Section>
        {
            new()
            {
                Id = "s1",
                Title = "Section 1",
                Prompts = new List<Prompt>
                {
                    new() { Id = "p1", Label = "Name" },
                    new() { Id = "p2", Label = "Age", Hints = new PromptHints { ExpectedDataType = "number" } },
                },
            },
        },
    };

    [Fact]
    public void NewShell_HasNoDocument_ShowsEmptyState()
    {
        var shell = CreateShell();

        shell.HasDocument.Should().BeFalse();
        shell.IsEmptyState.Should().BeTrue();
        shell.Title.Should().Be("PromptResponse");
    }

    [Fact]
    public async Task ExportPdf_WritesPdfContainingCurrentValues()
    {
        var fs = Substitute.For<IFileService>();
        var outPath = Path.Combine(Path.GetTempPath(), $"vm_export_{Guid.NewGuid():N}.pdf");
        fs.PickPdfExportPathAsync(Arg.Any<string>()).Returns(outPath);

        var session = new DocumentSessionService();
        var doc = MakeTemplate();
        doc.Sections[0].Prompts[0].Response = "Zaphod Beeblebrox";   // a current value
        session.Set(doc, null);
        var shell = CreateShell(fs, session: session);

        try
        {
            await shell.ExportPdf();

            File.Exists(outPath).Should().BeTrue();
            var bytes = await File.ReadAllBytesAsync(outPath);
            System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");

            using var pdf = Pdfe.Core.Document.PdfDocument.Open(bytes);
            var text = string.Concat(Enumerable.Range(1, pdf.Pages.Count).Select(i => pdf.GetPage(i).Text));
            text.Should().Contain("Zaphod Beeblebrox", "the export must capture the form's current values");
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Fact]
    public async Task ExportPdf_WhenCancelled_WritesNothing()
    {
        var fs = Substitute.For<IFileService>();
        fs.PickPdfExportPathAsync(Arg.Any<string>()).Returns((string?)null);
        var session = new DocumentSessionService();
        session.Set(MakeTemplate(), null);
        var shell = CreateShell(fs, session: session);

        await shell.ExportPdf();   // should be a no-op, not throw

        await fs.Received(1).PickPdfExportPathAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ExportPdfForm_WritesFillableAcroForm()
    {
        var fs = Substitute.For<IFileService>();
        var outPath = Path.Combine(Path.GetTempPath(), $"vm_form_{Guid.NewGuid():N}.pdf");
        fs.PickPdfExportPathAsync(Arg.Any<string>()).Returns(outPath);
        var session = new DocumentSessionService();
        session.Set(MakeTemplate(), null);
        var shell = CreateShell(fs, session: session);

        try
        {
            await shell.ExportPdfForm();

            using var pdf = Pdfe.Core.Document.PdfDocument.Open(await File.ReadAllBytesAsync(outPath));
            var form = pdf.GetAcroForm();
            form.Should().NotBeNull();
            form!.Fields.Should().NotBeEmpty();
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Fact]
    public async Task ExportHtml_WritesPageContainingCurrentValues()
    {
        var fs = Substitute.For<IFileService>();
        var outPath = Path.Combine(Path.GetTempPath(), $"vm_export_{Guid.NewGuid():N}.html");
        fs.PickExportPathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(outPath);

        var session = new DocumentSessionService();
        var doc = MakeTemplate();
        doc.Sections[0].Prompts[0].Response = "Zaphod Beeblebrox";   // a current value
        session.Set(doc, null);
        var shell = CreateShell(fs, session: session);

        try
        {
            await shell.ExportHtml();

            File.Exists(outPath).Should().BeTrue();
            var html = await File.ReadAllTextAsync(outPath);
            html.Should().StartWith("<!DOCTYPE html>");
            html.Should().Contain("Zaphod Beeblebrox", "the export must capture the form's current values");
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Fact]
    public async Task ExportHtmlForm_WritesInteractiveForm()
    {
        var fs = Substitute.For<IFileService>();
        var outPath = Path.Combine(Path.GetTempPath(), $"vm_form_{Guid.NewGuid():N}.html");
        fs.PickExportPathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(outPath);
        var session = new DocumentSessionService();
        session.Set(MakeTemplate(), null);
        var shell = CreateShell(fs, session: session);

        try
        {
            await shell.ExportHtmlForm();

            var html = await File.ReadAllTextAsync(outPath);
            html.Should().Contain("<form id=\"apr-form\"");
            html.Should().Contain("id=\"apr-download\"");
            html.Should().Contain(".aprf");
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Fact]
    public async Task ExportHtml_WhenCancelled_WritesNothing()
    {
        var fs = Substitute.For<IFileService>();
        fs.PickExportPathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns((string?)null);
        var session = new DocumentSessionService();
        session.Set(MakeTemplate(), null);
        var shell = CreateShell(fs, session: session);

        await shell.ExportHtml();   // should be a no-op, not throw

        await fs.Received(1).PickExportPathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ImportPdf_FillablePdf_LoadsTemplateIntoSessionAsDirty()
    {
        var fs = Substitute.For<IFileService>();
        var pdf = Path.Combine(Path.GetTempPath(), $"imp_{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(pdf, new FillablePdfDocumentRenderer().RenderToBytes(MakeTemplate()));
        fs.PickPdfImportPathAsync().Returns(pdf);

        var session = new DocumentSessionService();
        var shell = CreateShell(fs, session: session);

        try
        {
            await shell.ImportPdf();

            session.CurrentDocument.Should().NotBeNull("the imported PDF should become the open document");
            session.CurrentDocument!.DocumentType.Should().Be(DocumentType.Template);
            session.IsDirty.Should().BeTrue("an import is unsaved until the user picks a path");
        }
        finally
        {
            if (File.Exists(pdf)) File.Delete(pdf);
        }
    }

    [Fact]
    public async Task ImportPdf_WhenCancelled_NoOp()
    {
        var fs = Substitute.For<IFileService>();
        fs.PickPdfImportPathAsync().Returns((string?)null);
        var session = new DocumentSessionService();
        var shell = CreateShell(fs, session: session);

        await shell.ImportPdf();

        session.CurrentDocument.Should().BeNull();
        await fs.Received(1).PickPdfImportPathAsync();
    }

    [Fact]
    public async Task ImportPdf_FlatPdf_ShowsDialog_AndDoesNotOpen()
    {
        var fs = Substitute.For<IFileService>();
        var dlg = Substitute.For<IDialogService>();
        var pdf = Path.Combine(Path.GetTempPath(), $"flat_{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(pdf, new PdfDocumentRenderer().RenderToBytes(MakeTemplate()));
        fs.PickPdfImportPathAsync().Returns(pdf);

        var session = new DocumentSessionService();
        var shell = CreateShell(fs, dialogService: dlg, session: session);

        try
        {
            await shell.ImportPdf();

            session.CurrentDocument.Should().BeNull("a flat PDF has no fields to import");
            await dlg.Received(1).ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            if (File.Exists(pdf)) File.Delete(pdf);
        }
    }

    private static AprDocument ExprDoc() => new()
    {
        Metadata = new Metadata { Title = "T" },
        Sections = new List<Section>
        {
            new()
            {
                Id = "s", Title = "S",
                Prompts = new List<Prompt>
                {
                    new() { Id = "status", Label = "Status", Response = "Employed" },
                    new() { Id = "employer", Label = "Employer", Hints = new PromptHints { ExprHidden = "status == 'Retired'" } },
                    new() { Id = "qty", Label = "Qty", Response = "2" },
                    new() { Id = "price", Label = "Price", Response = "3" },
                    new() { Id = "total", Label = "Total", Hints = new PromptHints { ExprValue = "double(qty) * double(price)" } },
                },
            },
        },
    };

    [Fact]
    public void Expressions_ConditionalVisibility_UpdatesAsDriverChanges()
    {
        var session = new DocumentSessionService();
        var shell = CreateShell(session: session);
        session.Set(ExprDoc(), null);   // set after the shell subscribes to DocumentChanged

        var status = shell.PromptViewModels.Single(p => p.Id == "status");
        var employer = shell.PromptViewModels.Single(p => p.Id == "employer");

        employer.IsVisible.Should().BeTrue("status is Employed at load");

        status.Response = "Retired";
        employer.IsVisible.Should().BeFalse("exprHidden becomes true");

        status.Response = "Employed";
        employer.IsVisible.Should().BeTrue("driver reverted");
    }

    [Fact]
    public void Expressions_ComputedField_IsReadOnly_AndRecomputesLive()
    {
        var session = new DocumentSessionService();
        var shell = CreateShell(session: session);
        session.Set(ExprDoc(), null);   // set after the shell subscribes to DocumentChanged

        var qty = shell.PromptViewModels.Single(p => p.Id == "qty");
        var total = shell.PromptViewModels.Single(p => p.Id == "total");

        total.Response.Should().Be("6", "2 * 3 computed at load");
        total.IsReadOnly.Should().BeTrue("computed fields are read-only");
        total.IsInputEnabled.Should().BeFalse();

        qty.Response = "5";
        total.Response.Should().Be("15", "recomputed live when a dependency changes");
    }

    [Fact]
    public void RefreshAdvisories_SurfacesCrossFieldValidationMessages()
    {
        var session = new DocumentSessionService();
        var doc = new AprDocument
        {
            Metadata = new Metadata { Title = "T" },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "s", Title = "S",
                    Prompts = new List<Prompt>
                    {
                        new() { Id = "start", Label = "Start", Response = "2021-01-01" },
                        new()
                        {
                            Id = "end", Label = "End", Response = "2020-01-01",
                            Hints = new PromptHints
                            {
                                ExprValidation = "_this == '' || start == '' ? '' : (timestamp(_this) > timestamp(start) ? '' : 'End must be after start')",
                            },
                        },
                    },
                },
            },
        };
        session.Set(doc, null);
        var shell = CreateShell(session: session);

        shell.RefreshAdvisories();

        shell.Advisories.Should().Contain(a => a.PromptId == "end" && a.Message == "End must be after start");

        // Fix the value → the validation advisory clears.
        doc.Sections[0].Prompts[1].Response = "2022-01-01";
        shell.RefreshAdvisories();
        shell.Advisories.Should().NotContain(a => a.Message == "End must be after start");
    }

    [Fact]
    public void FocusAdvisory_RaisesFocusPromptRequested_WithThePromptId()
    {
        var shell = CreateShell();
        string? requested = null;
        shell.FocusPromptRequested += id => requested = id;

        shell.FocusAdvisoryCommand.Execute("prompt_42");

        requested.Should().Be("prompt_42");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FocusAdvisory_BlankId_IsNoOp(string? id)
    {
        var shell = CreateShell();
        var raised = false;
        shell.FocusPromptRequested += _ => raised = true;

        shell.FocusAdvisoryCommand.Execute(id);

        raised.Should().BeFalse();
    }

    [Fact]
    public void ExportCommands_AreDisabled_WithoutADocument()
    {
        var shell = CreateShell();

        shell.ExportPdfCommand.CanExecute(null).Should().BeFalse();
        shell.ExportPdfFormCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void NewShell_WithRecentFiles_ExposesThemForTheHomeScreen()
    {
        var recent = new RecentFilesService();
        recent.Add("/forms/intake.aprt", "Intake");

        var shell = CreateShell(recentFiles: recent);

        shell.HasRecentFiles.Should().BeTrue();
        shell.RecentFiles.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Path = "/forms/intake.aprt", DisplayName = "Intake" });
    }

    [Fact]
    public async Task Open_AddsTheFileToRecent()
    {
        var fs = Substitute.For<IFileService>();
        fs.OpenFileAsync().Returns(MakeTemplate());
        fs.CurrentFilePath.Returns("/forms/test.aprt");
        var shell = CreateShell(fs);

        await shell.Open();

        shell.RecentFiles.Select(r => r.Path).Should().Contain("/forms/test.aprt");
        shell.HasRecentFiles.Should().BeTrue();
    }

    [Fact]
    public async Task OpenRecent_LoadsDocumentAndSetsCurrentPath()
    {
        var fs = Substitute.For<IFileService>();
        fs.LoadFileAsync("/forms/saved.aprf").Returns(MakeTemplate());
        var session = new DocumentSessionService();
        var shell = CreateShell(fs, session: session);

        await shell.OpenRecent("/forms/saved.aprf");

        session.HasDocument.Should().BeTrue();
        fs.Received(1).SetCurrentFilePath("/forms/saved.aprf");
    }

    [Fact]
    public void NewShell_WithStarterTemplates_ExposesThemForTheHomeScreen()
    {
        var catalog = Substitute.For<ITemplateCatalogService>();
        catalog.Templates.Returns(new[] { new StarterTemplate("Time-Off Request", "/t/time-off.aprt", "d") });

        var shell = CreateShell(templateCatalog: catalog);

        shell.HasStarterTemplates.Should().BeTrue();
        shell.StarterTemplates.Should().ContainSingle()
            .Which.DisplayName.Should().Be("Time-Off Request");
    }

    [Fact]
    public async Task NewFromTemplate_LoadsAFreshUnsavedDocument()
    {
        var fs = Substitute.For<IFileService>();
        fs.LoadFileAsync("/t/time-off.aprt").Returns(MakeTemplate());
        var session = new DocumentSessionService();
        var shell = CreateShell(fs, session: session);

        await shell.NewFromTemplate("/t/time-off.aprt");

        session.HasDocument.Should().BeTrue();
        // A fresh copy: the source path is cleared so the first save is "Save As".
        fs.Received(1).ClearCurrentFilePath();
    }

    [Fact]
    public async Task OpenRecent_MissingFile_IsNoOp()
    {
        var fs = Substitute.For<IFileService>();
        fs.LoadFileAsync(Arg.Any<string>()).Returns((AprDocument?)null);   // file gone
        var session = new DocumentSessionService();
        var shell = CreateShell(fs, session: session);

        await shell.OpenRecent("/forms/gone.aprf");

        session.HasDocument.Should().BeFalse();
    }

    [Fact]
    public void NewTemplate_CreatesBlankTemplate_AndExitsEmptyState()
    {
        var shell = CreateShell();

        shell.NewTemplateCommand.Execute(null);

        shell.HasDocument.Should().BeTrue();
        shell.IsEmptyState.Should().BeFalse();
        shell.Mode.Should().Be(DocumentMode.EditingTemplate);
    }

    [Fact]
    public async Task OpenCommand_LoadsDocumentViaFileService_AndUpdatesShell()
    {
        var fs = Substitute.For<IFileService>();
        fs.OpenFileAsync().Returns(MakeTemplate());
        fs.CurrentFilePath.Returns("/tmp/test.aprt");
        var shell = CreateShell(fileService: fs);

        await shell.Open();

        shell.HasDocument.Should().BeTrue();
        shell.CurrentDocumentTitle.Should().Be("Test Template");
        _ = fs.Received(1).OpenFileAsync();
    }

    [Fact]
    public async Task OpenFromPath_LoadsViaFileService_AndUpdatesShell()
    {
        var fs = Substitute.For<IFileService>();
        fs.LoadFileAsync("/tmp/example.aprt").Returns(MakeTemplate());
        var shell = CreateShell(fileService: fs);

        await shell.OpenFromPath("/tmp/example.aprt");

        shell.HasDocument.Should().BeTrue();
        shell.CurrentDocumentTitle.Should().Be("Test Template");
        _ = fs.Received(1).LoadFileAsync("/tmp/example.aprt");
    }

    [Fact]
    public async Task OpenFromPath_WhenFileFailsToLoad_LeavesShellUnchanged()
    {
        var fs = Substitute.For<IFileService>();
        fs.LoadFileAsync(Arg.Any<string>()).Returns((AprDocument?)null);
        var shell = CreateShell(fileService: fs);

        await shell.OpenFromPath("/nonexistent.aprt");

        shell.HasDocument.Should().BeFalse();
    }

    [Fact]
    public async Task OpenCommand_WhenUserCancels_LeavesShellUnchanged()
    {
        var fs = Substitute.For<IFileService>();
        fs.OpenFileAsync().Returns((AprDocument?)null);
        var shell = CreateShell(fileService: fs);

        await shell.Open();

        shell.HasDocument.Should().BeFalse();
    }

    [Fact]
    public async Task SaveCommand_RequiresDocument_NoOpWhenEmpty()
    {
        var fs = Substitute.For<IFileService>();
        var shell = CreateShell(fileService: fs);

        await shell.Save();

        _ = fs.DidNotReceive().SaveFileAsync(Arg.Any<AprDocument>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SaveCommand_WithCurrentFilePath_SavesToPath()
    {
        var fs = Substitute.For<IFileService>();
        fs.CurrentFilePath.Returns("/tmp/example.aprt");
        var session = new DocumentSessionService();
        var shell = CreateShell(fileService: fs, session: session);
        session.Set(MakeTemplate(), "/tmp/example.aprt", dirty: true);

        await shell.Save();

        _ = fs.Received(1).SaveFileAsync(Arg.Any<AprDocument>(), "/tmp/example.aprt");
    }

    [Fact]
    public async Task SaveCommand_WithoutPath_FallsBackToSaveAs()
    {
        var fs = Substitute.For<IFileService>();
        fs.CurrentFilePath.Returns((string?)null);
        var session = new DocumentSessionService();
        var shell = CreateShell(fileService: fs, session: session);
        session.Set(MakeTemplate(), null, dirty: true);

        await shell.Save();

        _ = fs.Received(1).SaveFileAsAsync(Arg.Any<AprDocument>());
    }

    [Fact]
    public async Task SaveCommand_AfterSuccessfulSave_ClearsDirty()
    {
        var fs = Substitute.For<IFileService>();
        fs.CurrentFilePath.Returns("/tmp/x.aprt");
        var session = new DocumentSessionService();
        var shell = CreateShell(fileService: fs, session: session);
        session.Set(MakeTemplate(), "/tmp/x.aprt", dirty: true);

        await shell.Save();

        session.IsDirty.Should().BeFalse();
    }

    [Fact]
    public async Task CloseCommand_OnDirtyDocument_AsksToConfirm()
    {
        var dialog = Substitute.For<IDialogService>();
        dialog.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var session = new DocumentSessionService();
        var shell = CreateShell(dialogService: dialog, session: session);
        session.Set(MakeTemplate(), "/tmp/x.aprt", dirty: true);

        await shell.Close();

        _ = dialog.Received(1).ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>());
        session.HasDocument.Should().BeFalse();
    }

    [Fact]
    public async Task CloseCommand_OnDirtyDocument_UserCancels_KeepsDocument()
    {
        var dialog = Substitute.For<IDialogService>();
        dialog.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var session = new DocumentSessionService();
        var shell = CreateShell(dialogService: dialog, session: session);
        session.Set(MakeTemplate(), "/tmp/x.aprt", dirty: true);

        await shell.Close();

        session.HasDocument.Should().BeTrue("user declined the close");
    }

    [Fact]
    public void DocumentChanged_BindsFormProgress_ToNewDocument()
    {
        var session = new DocumentSessionService();
        var shell = CreateShell(session: session);

        session.Set(MakeTemplate(), null);

        shell.Progress.TotalPrompts.Should().Be(2);
    }

    [Fact]
    public void DocumentChanged_BindsSearch_ToNewDocument()
    {
        var session = new DocumentSessionService();
        var shell = CreateShell(session: session);
        session.Set(MakeTemplate(), null);

        shell.Search.Query = "name";

        shell.Search.Matches.Should().ContainSingle();
    }

    [Fact]
    public void DocumentChanged_PopulatesPromptVms_FromFactory()
    {
        var session = new DocumentSessionService();
        var shell = CreateShell(session: session);
        session.Set(MakeTemplate(), null);

        shell.PromptViewModels.Should().HaveCount(2);
        shell.PromptViewModels.Should().Contain(vm => vm is NumberPromptViewModel);
        shell.PromptViewModels.Should().Contain(vm => vm is TextPromptViewModel);
    }

    [Fact]
    public void DocumentChanged_OnClose_ClearsPromptVms_AndShowsEmptyState()
    {
        var session = new DocumentSessionService();
        var shell = CreateShell(session: session);
        session.Set(MakeTemplate(), null);
        shell.PromptViewModels.Should().NotBeEmpty();

        session.Close();

        shell.PromptViewModels.Should().BeEmpty();
        shell.IsEmptyState.Should().BeTrue();
    }

    [Fact]
    public void Title_TracksDocumentSession()
    {
        var session = new DocumentSessionService();
        var shell = CreateShell(session: session);

        shell.Title.Should().Be("PromptResponse");

        session.Set(MakeTemplate(), null);
        shell.Title.Should().Contain("Test Template");

        session.MarkDirty();
        shell.Title.Should().Contain("•");
    }

    [Fact]
    public void StatusMessage_AnnouncesStateChanges()
    {
        var session = new DocumentSessionService();
        var shell = CreateShell(session: session);

        shell.StatusMessage.Should().Contain("No document");

        session.Set(MakeTemplate(), null);
        shell.StatusMessage.Should().Contain("Test Template");
    }

    [Fact]
    public void Profile_IsAvailableForBindings()
    {
        var profile = new ProfileService(new StubProbe(), applyAffordanceDefaults: false);
        var shell = CreateShell(profile: profile);

        shell.ProfileService.Should().BeSameAs(profile);
    }

    [Fact]
    public void Constructor_RejectsAllNullArguments()
    {
        var fs = Substitute.For<IFileService>();
        var dlg = Substitute.For<IDialogService>();
        var session = new DocumentSessionService();
        var profile = new ProfileService(new StubProbe(), applyAffordanceDefaults: false);
        var factory = new PromptViewModelFactory(profile);

        Action a = () => new MainShellViewModel(null!, dlg, session, profile, factory);
        Action b = () => new MainShellViewModel(fs, null!, session, profile, factory);
        Action c = () => new MainShellViewModel(fs, dlg, null!, profile, factory);
        Action d = () => new MainShellViewModel(fs, dlg, session, null!, factory);
        Action e = () => new MainShellViewModel(fs, dlg, session, profile, null!);

        a.Should().Throw<ArgumentNullException>();
        b.Should().Throw<ArgumentNullException>();
        c.Should().Throw<ArgumentNullException>();
        d.Should().Throw<ArgumentNullException>();
        e.Should().Throw<ArgumentNullException>();
    }
}
