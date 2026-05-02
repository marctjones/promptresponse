using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
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
        IProfileService? profile = null)
    {
        fileService ??= Substitute.For<IFileService>();
        dialogService ??= Substitute.For<IDialogService>();
        session ??= new DocumentSessionService();
        profile ??= new ProfileService(new StubProbe(), applyAffordanceDefaults: false);
        var factory = new PromptViewModelFactory(profile);
        return new MainShellViewModel(fileService, dialogService, session, profile, factory);
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
