using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Services;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>Session, recent-file, and template-catalog behavior for the desktop shell.</summary>
public partial class MainShellViewModelTests
{
    [Fact]
    public void NewShell_WithRecentFiles_ExposesThemForTheHomeScreen()
    {
        var recent = new RecentFilesService();
        recent.Add("/forms/intake.aprt", "Intake");
        var shell = CreateShell(recentFiles: recent);
        shell.HasRecentFiles.Should().BeTrue();
        shell.RecentFiles.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Path = "/forms/intake.aprt", DisplayName = "Intake" });
    }

    [Fact]
    public async Task Open_AddsTheFileToRecent()
    {
        var fs = Substitute.For<IFileService>(); fs.OpenFileAsync().Returns(MakeTemplate()); fs.CurrentFilePath.Returns("/forms/test.aprt");
        var shell = CreateShell(fs); await shell.Open();
        shell.RecentFiles.Select(r => r.Path).Should().Contain("/forms/test.aprt"); shell.HasRecentFiles.Should().BeTrue();
    }

    [Fact]
    public async Task OpenRecent_LoadsDocumentAndSetsCurrentPath()
    {
        var fs = Substitute.For<IFileService>(); fs.LoadFileAsync("/forms/saved.aprf").Returns(MakeTemplate());
        var session = new DocumentSessionService(); var shell = CreateShell(fs, session: session);
        await shell.OpenRecent("/forms/saved.aprf");
        session.HasDocument.Should().BeTrue(); fs.Received(1).SetCurrentFilePath("/forms/saved.aprf");
    }

    [Fact]
    public void NewShell_WithStarterTemplates_ExposesThemForTheHomeScreen()
    {
        var catalog = Substitute.For<ITemplateCatalogService>(); catalog.Templates.Returns(new[] { new StarterTemplate("Time-Off Request", "/t/time-off.aprt", "d") });
        var shell = CreateShell(templateCatalog: catalog);
        shell.HasStarterTemplates.Should().BeTrue(); shell.StarterTemplates.Should().ContainSingle().Which.DisplayName.Should().Be("Time-Off Request");
    }

    [Fact]
    public async Task NewFromTemplate_LoadsAFreshUnsavedDocument()
    {
        var fs = Substitute.For<IFileService>(); fs.LoadFileAsync("/t/time-off.aprt").Returns(MakeTemplate());
        var session = new DocumentSessionService(); var shell = CreateShell(fs, session: session);
        await shell.NewFromTemplate("/t/time-off.aprt");
        session.HasDocument.Should().BeTrue(); fs.Received(1).ClearCurrentFilePath();
    }

    [Fact]
    public async Task OpenRecent_MissingFile_IsNoOp()
    {
        var fs = Substitute.For<IFileService>(); fs.LoadFileAsync(Arg.Any<string>()).Returns((AprDocument?)null);
        var session = new DocumentSessionService(); var shell = CreateShell(fs, session: session);
        await shell.OpenRecent("/forms/gone.aprf"); session.HasDocument.Should().BeFalse();
    }

    [Fact]
    public void NewTemplate_CreatesBlankTemplate_AndExitsEmptyState()
    {
        var shell = CreateShell(); shell.NewTemplateCommand.Execute(null);
        shell.HasDocument.Should().BeTrue(); shell.IsEmptyState.Should().BeFalse(); shell.Mode.Should().Be(DocumentMode.EditingTemplate);
    }

    [Fact]
    public async Task OpenCommand_LoadsDocumentViaFileService_AndUpdatesShell()
    {
        var fs = Substitute.For<IFileService>(); fs.OpenFileAsync().Returns(MakeTemplate()); fs.CurrentFilePath.Returns("/tmp/test.aprt");
        var shell = CreateShell(fileService: fs); await shell.Open();
        shell.HasDocument.Should().BeTrue(); shell.CurrentDocumentTitle.Should().Be("Test Template"); _ = fs.Received(1).OpenFileAsync();
    }

    [Fact]
    public async Task OpenFromPath_LoadsViaFileService_AndUpdatesShell()
    {
        var fs = Substitute.For<IFileService>(); fs.LoadFileAsync("/tmp/example.aprt").Returns(MakeTemplate());
        var shell = CreateShell(fileService: fs); await shell.OpenFromPath("/tmp/example.aprt");
        shell.HasDocument.Should().BeTrue(); shell.CurrentDocumentTitle.Should().Be("Test Template"); _ = fs.Received(1).LoadFileAsync("/tmp/example.aprt");
    }

    [Fact]
    public async Task OpenFromPath_WhenFileFailsToLoad_LeavesShellUnchanged()
    {
        var fs = Substitute.For<IFileService>(); fs.LoadFileAsync(Arg.Any<string>()).Returns((AprDocument?)null);
        var shell = CreateShell(fileService: fs); await shell.OpenFromPath("/nonexistent.aprt"); shell.HasDocument.Should().BeFalse();
    }

    [Fact]
    public async Task OpenCommand_WhenUserCancels_LeavesShellUnchanged()
    {
        var fs = Substitute.For<IFileService>(); fs.OpenFileAsync().Returns((AprDocument?)null);
        var shell = CreateShell(fileService: fs); await shell.Open(); shell.HasDocument.Should().BeFalse();
    }

    [Fact]
    public async Task SaveCommand_RequiresDocument_NoOpWhenEmpty()
    {
        var fs = Substitute.For<IFileService>(); var shell = CreateShell(fileService: fs); await shell.Save();
        _ = fs.DidNotReceive().SaveFileAsync(Arg.Any<AprDocument>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SaveCommand_WithCurrentFilePath_SavesToPath()
    {
        var fs = Substitute.For<IFileService>(); fs.CurrentFilePath.Returns("/tmp/example.aprt"); var session = new DocumentSessionService();
        var shell = CreateShell(fileService: fs, session: session); session.Set(MakeTemplate(), "/tmp/example.aprt", dirty: true); await shell.Save();
        _ = fs.Received(1).SaveFileAsync(Arg.Any<AprDocument>(), "/tmp/example.aprt");
    }

    [Fact]
    public async Task SaveCommand_WithoutPath_FallsBackToSaveAs()
    {
        var fs = Substitute.For<IFileService>(); fs.CurrentFilePath.Returns((string?)null); var session = new DocumentSessionService();
        var shell = CreateShell(fileService: fs, session: session); session.Set(MakeTemplate(), null, dirty: true); await shell.Save();
        _ = fs.Received(1).SaveFileAsAsync(Arg.Any<AprDocument>());
    }

    [Fact]
    public async Task SaveCommand_AfterSuccessfulSave_ClearsDirty()
    {
        var fs = Substitute.For<IFileService>(); fs.CurrentFilePath.Returns("/tmp/x.aprt"); var session = new DocumentSessionService();
        var shell = CreateShell(fileService: fs, session: session); session.Set(MakeTemplate(), "/tmp/x.aprt", dirty: true); await shell.Save(); session.IsDirty.Should().BeFalse();
    }

    [Fact]
    public async Task CloseCommand_OnDirtyDocument_AsksToConfirm()
    {
        var dialog = Substitute.For<IDialogService>(); dialog.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(true); var session = new DocumentSessionService();
        var shell = CreateShell(dialogService: dialog, session: session); session.Set(MakeTemplate(), "/tmp/x.aprt", dirty: true); await shell.Close();
        _ = dialog.Received(1).ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>()); session.HasDocument.Should().BeFalse();
    }

    [Fact]
    public async Task CloseCommand_OnDirtyDocument_UserCancels_KeepsDocument()
    {
        var dialog = Substitute.For<IDialogService>(); dialog.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(false); var session = new DocumentSessionService();
        var shell = CreateShell(dialogService: dialog, session: session); session.Set(MakeTemplate(), "/tmp/x.aprt", dirty: true); await shell.Close();
        session.HasDocument.Should().BeTrue("user declined the close");
    }
}
