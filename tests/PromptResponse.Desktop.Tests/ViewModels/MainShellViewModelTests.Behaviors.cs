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

/// <summary>Shell state, advisory, expression, and submission behavior.</summary>
public partial class MainShellViewModelTests
{
    [Fact]
    public void NewShell_HasNoDocument_ShowsEmptyState()
    {
        var shell = CreateShell(); shell.HasDocument.Should().BeFalse(); shell.IsEmptyState.Should().BeTrue(); shell.Title.Should().Be("PromptResponse");
    }

    [Fact]
    public async Task OpenFromPath_ForFilling_ShowsTemplateAsFormInsteadOfEditor()
    {
        var file = Path.GetTempFileName();
        try { await File.WriteAllTextAsync(file, "{\"version\":\"1.0-beta\",\"documentType\":\"template\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"Name\",\"response\":\"\"}]}]}"); var shell = CreateShell(); await shell.OpenFromPath(file, openForFilling: true); shell.IsEditMode.Should().BeFalse(); shell.ShowFullFillList.Should().BeTrue(); } finally { File.Delete(file); }
    }

    private static AprDocument ExprDoc() => new()
    {
        Metadata = new Metadata { Title = "T" },
        Sections = [new Section { Id = "s", Title = "S", Prompts = [new Prompt { Id = "status", Label = "Status", Response = "Employed" }, new Prompt { Id = "employer", Label = "Employer", Hints = new PromptHints { ExprHidden = "status == 'Retired'" } }, new Prompt { Id = "qty", Label = "Qty", Response = "2", Hints = new PromptHints { ExpectedDataType = "number" } }, new Prompt { Id = "price", Label = "Price", Response = "3", Hints = new PromptHints { ExpectedDataType = "number" } }, new Prompt { Id = "total", Label = "Total", Hints = new PromptHints { ExpectedDataType = "number", ExprValue = "qty * price" } }] }],
    };

    [Fact]
    public void Expressions_ConditionalVisibility_UpdatesAsDriverChanges()
    {
        var session = new DocumentSessionService(); var shell = CreateShell(session: session); session.Set(ExprDoc(), null); var status = shell.PromptViewModels.Single(p => p.Id == "status"); var employer = shell.PromptViewModels.Single(p => p.Id == "employer"); employer.IsVisible.Should().BeTrue("status is Employed at load"); status.Response = "Retired"; employer.IsVisible.Should().BeFalse("exprHidden becomes true"); status.Response = "Employed"; employer.IsVisible.Should().BeTrue("driver reverted");
    }

    [Fact]
    public void Expressions_ComputedField_StaysEditable_AndRecomputesLive()
    {
        var session = new DocumentSessionService(); var shell = CreateShell(session: session); session.Set(ExprDoc(), null); var qty = shell.PromptViewModels.Single(p => p.Id == "qty"); var total = shell.PromptViewModels.Single(p => p.Id == "total"); total.Response.Should().Be("6", "2 * 3 computed at load"); total.IsReadOnly.Should().BeFalse("a computed field stays editable (specification section 8.6)"); total.IsInputEnabled.Should().BeTrue(); qty.Response = "5"; total.Response.Should().Be("15", "an untouched computed field keeps tracking its inputs");
    }

    [Fact]
    public void RefreshAdvisories_SurfacesCrossFieldValidationMessages()
    {
        var session = new DocumentSessionService(); var doc = new AprDocument { Metadata = new Metadata { Title = "T" }, Sections = [new Section { Id = "s", Title = "S", Prompts = [new Prompt { Id = "start", Label = "Start", Response = "2021-01-01", Hints = new PromptHints { ExpectedDataType = "date" } }, new Prompt { Id = "end", Label = "End", Response = "2020-01-01", Hints = new PromptHints { ExpectedDataType = "date", ExprValidation = "_this > start ? '' : 'End must be after start'" } }] }] }; session.Set(doc, null); var shell = CreateShell(session: session);
        shell.RefreshAdvisories(); shell.Advisories.Should().Contain(a => a.PromptId == "end" && a.Message == "End must be after start"); doc.Sections[0].Prompts[1].Response = "2022-01-01"; shell.RefreshAdvisories(); shell.Advisories.Should().NotContain(a => a.Message == "End must be after start");
    }

    [Fact]
    public void RefreshAdvisories_SurfacesBidiOverrideWithoutChangingResponse()
    {
        var session = new DocumentSessionService(); var doc = MakeTemplate(); doc.Sections[0].Prompts[0].Response = "safe\u202Etxt.exe"; session.Set(doc, null); var shell = CreateShell(session: session);
        shell.RefreshAdvisories(); shell.Advisories.Should().Contain(a => a.PromptId == "p1" && a.Message.Contains("bidirectional override") && a.Message.Contains("U+202E") && a.Message.Contains("offset 4"), "the advisory is the visible codepoint/offset inspector linked to its prompt"); doc.Sections[0].Prompts[0].Response.Should().Be("safe\u202Etxt.exe");
    }

    [Fact]
    public void FocusAdvisory_RaisesFocusPromptRequested_WithThePromptId()
    {
        var shell = CreateShell(); string? requested = null; shell.FocusPromptRequested += id => requested = id; shell.FocusAdvisoryCommand.Execute("prompt_42"); requested.Should().Be("prompt_42");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FocusAdvisory_BlankId_IsNoOp(string? id)
    {
        var shell = CreateShell(); var raised = false; shell.FocusPromptRequested += _ => raised = true; shell.FocusAdvisoryCommand.Execute(id); raised.Should().BeFalse();
    }

    [Fact]
    public void ExportCommands_AreDisabled_WithoutADocument()
    {
        var shell = CreateShell(); shell.PrintPreviewCommand.CanExecute(null).Should().BeFalse(); shell.ExportPdfCommand.CanExecute(null).Should().BeFalse(); shell.ExportPdfFormCommand.CanExecute(null).Should().BeFalse(); shell.ExportHtmlCommand.CanExecute(null).Should().BeFalse(); shell.ExportHtmlFormCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void DocumentChanged_BindsFormProgress_ToNewDocument()
    {
        var session = new DocumentSessionService(); var shell = CreateShell(session: session); session.Set(MakeTemplate(), null); shell.Progress.TotalPrompts.Should().Be(2);
    }

    [Fact]
    public void DocumentChanged_BindsSearch_ToNewDocument()
    {
        var session = new DocumentSessionService(); var shell = CreateShell(session: session); session.Set(MakeTemplate(), null); shell.Search.Query = "name"; shell.Search.Matches.Should().ContainSingle();
    }

    [Fact]
    public void DocumentChanged_PopulatesPromptVms_FromFactory()
    {
        var session = new DocumentSessionService(); var shell = CreateShell(session: session); session.Set(MakeTemplate(), null); shell.PromptViewModels.Should().HaveCount(2); shell.PromptViewModels.Should().Contain(vm => vm is NumberPromptViewModel); shell.PromptViewModels.Should().Contain(vm => vm is TextPromptViewModel);
    }

    [Fact]
    public void DocumentChanged_OnClose_ClearsPromptVms_AndShowsEmptyState()
    {
        var session = new DocumentSessionService(); var shell = CreateShell(session: session); session.Set(MakeTemplate(), null); shell.PromptViewModels.Should().NotBeEmpty(); session.Close(); shell.PromptViewModels.Should().BeEmpty(); shell.IsEmptyState.Should().BeTrue();
    }

    [Fact]
    public void Title_TracksDocumentSession()
    {
        var session = new DocumentSessionService(); var shell = CreateShell(session: session); shell.Title.Should().Be("PromptResponse"); session.Set(MakeTemplate(), null); shell.Title.Should().Contain("Test Template"); session.MarkDirty(); shell.Title.Should().Contain("•");
    }

    [Fact]
    public void StatusMessage_AnnouncesStateChanges()
    {
        var session = new DocumentSessionService(); var shell = CreateShell(session: session); shell.StatusMessage.Should().Contain("No document"); session.Set(MakeTemplate(), null); shell.StatusMessage.Should().Contain("Test Template");
    }

    [Fact]
    public void Profile_IsAvailableForBindings()
    {
        var profile = new ProfileService(new StubProbe(), applyAffordanceDefaults: false); var shell = CreateShell(profile: profile); shell.ProfileService.Should().BeSameAs(profile);
    }

    [Fact]
    public void Constructor_RejectsAllNullArguments()
    {
        var fs = Substitute.For<IFileService>(); var dlg = Substitute.For<IDialogService>(); var session = new DocumentSessionService(); var profile = new ProfileService(new StubProbe(), applyAffordanceDefaults: false); var factory = new PromptViewModelFactory(profile);
        new Action(() => new MainShellViewModel(null!, dlg, session, profile, factory)).Should().Throw<ArgumentNullException>(); new Action(() => new MainShellViewModel(fs, null!, session, profile, factory)).Should().Throw<ArgumentNullException>(); new Action(() => new MainShellViewModel(fs, dlg, null!, profile, factory)).Should().Throw<ArgumentNullException>(); new Action(() => new MainShellViewModel(fs, dlg, session, null!, factory)).Should().Throw<ArgumentNullException>(); new Action(() => new MainShellViewModel(fs, dlg, session, profile, null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SubmitViaEmail_SavesFilledCopyAndHandsItToSelectedMailTarget()
    {
        var fileService = Substitute.For<IFileService>(); var dialogs = Substitute.For<IDialogService>(); var handoff = Substitute.For<IMailHandoffService>(); var session = new DocumentSessionService(); var profile = new ProfileService(new StubProbe(), applyAffordanceDefaults: false); var document = MakeTemplate(); document.DocumentType = DocumentType.FilledForm; document.Metadata.SubmissionUrls = ["mailto:forms@example.com"]; session.Set(document, null, dirty: true); fileService.PickExportPathAsync(Arg.Any<string>(), "Save completed APR file", "APR Filled Form", "aprf").Returns(Task.FromResult<string?>(Path.Combine(Path.GetTempPath(), "completed.aprf"))); dialogs.ShowChoiceAsync("Submit via email", Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns(Task.FromResult<int?>(0)); dialogs.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.FromResult(true)); handoff.ComposeAsync(Arg.Any<MailHandoffRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new MailHandoffResult(true, false, "Draft opened.")));
        var shell = new MainShellViewModel(fileService, dialogs, session, profile, new PromptViewModelFactory(profile), serializer: new AprJsonSerializer(), mailHandoff: handoff); shell.CanSubmitViaEmail().Should().BeTrue(); await shell.SubmitViaEmail(); await fileService.Received(1).SaveFileAsync(Arg.Is<AprDocument>(copy => copy.DocumentType == DocumentType.FilledForm && !ReferenceEquals(copy, document)), Arg.Any<string>()); await handoff.Received(1).ComposeAsync(Arg.Is<MailHandoffRequest>(request => request.MailtoTarget == "mailto:forms@example.com" && request.AttachmentPath.EndsWith("completed.aprf")), Arg.Any<CancellationToken>()); document.DocumentType.Should().Be(DocumentType.FilledForm, "the outgoing copy must not mutate the open document");
    }
}
