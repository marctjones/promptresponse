using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.Workflows;

/// <summary>
/// Whole tasks, start to finish, through real files on disk.
/// </summary>
/// <remarks>
/// <para>
/// The suite had 487 test methods and deep coverage of individual commands - Save with a
/// path, Save without one, Export cancelled, Import declined - plus a driver that
/// activates every control in the window. What none of it did was carry a task from
/// beginning to end: author a template and save it, fill one in and save it as a filled
/// form, build a table and put values in the cells. Every step was proven in isolation
/// and no journey was proven at all.
/// </para>
/// <para>
/// These go through the real FileService and a real temporary directory, so a journey
/// that claims to have saved something has actually written bytes, and reloading them is
/// what the assertion reads. A mocked file service can only confirm that a method was
/// called; it cannot notice that what landed on disk was not what the user built.
/// </para>
/// </remarks>
public class WorkflowTests : IDisposable
{
    private sealed class Probe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    /// <summary>
    /// A real file service that answers the pickers instead of opening a dialog.
    /// </summary>
    /// <remarks>
    /// Load and save delegate to the genuine implementation and land on disk, so a
    /// journey that says it exported something has written bytes somebody can open. Only
    /// the three "ask the user where" calls are scripted, because a headless test has
    /// nobody to ask - and mocking the whole service would mean asserting that a method
    /// was called rather than that the right file exists.
    ///
    /// Delegation rather than inheritance: the shell calls through IFileService, and
    /// FileService's methods are not virtual, so hiding them with `new` would have been
    /// silently bypassed.
    /// </remarks>
    private sealed class ScriptedFileService(IAprSerializer serializer) : IFileService
    {
        private readonly FileService _real = new(serializer);

        public string? NextExportPath { get; set; }
        public string? NextCertificatePath { get; set; }

        public Task<AprDocument?> OpenFileAsync() => _real.OpenFileAsync();
        public Task<AprDocument?> LoadFileAsync(string filePath) => _real.LoadFileAsync(filePath);
        public Task<bool> SaveFileAsAsync(AprDocument document) => _real.SaveFileAsAsync(document);
        public Task SaveFileAsync(AprDocument document, string filePath) => _real.SaveFileAsync(document, filePath);
        public Task<string?> PickPdfImportPathAsync() => _real.PickPdfImportPathAsync();
        public string? CurrentFilePath => _real.CurrentFilePath;
        public void ClearCurrentFilePath() => _real.ClearCurrentFilePath();
        public void SetCurrentFilePath(string filePath) => _real.SetCurrentFilePath(filePath);

        public Task<string?> PickPdfExportPathAsync(string suggestedFileName) =>
            Task.FromResult(NextExportPath);
        public Task<string?> PickExportPathAsync(
            string suggestedFileName, string title, string typeLabel, string extension) =>
            Task.FromResult(NextExportPath);
        public Task<string?> PickCertificateAsync() => Task.FromResult(NextCertificatePath);
    }

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "apr-flow-" + Guid.NewGuid().ToString("N"));
    private readonly AprJsonSerializer _serializer = new();
    private readonly ScriptedFileService _files;
    private readonly DocumentSessionService _session = new();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly MainShellViewModel _shell;

    public WorkflowTests()
    {
        Directory.CreateDirectory(_dir);
        _files = new ScriptedFileService(_serializer);
        var profile = new ProfileService(new Probe(), applyAffordanceDefaults: false);
        _shell = new MainShellViewModel(_files, _dialogs, _session, profile,
            new PromptViewModelFactory(profile));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    private string Path_(string name) => Path.Combine(_dir, name);

    private static string RepositoryPath(params string[] parts) =>
        Path.Combine([Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..")), .. parts]);

    /// <summary>Reads back what actually landed on disk.</summary>
    private AprDocument Reload(string path)
    {
        File.Exists(path).Should().BeTrue($"the workflow claimed to save {Path.GetFileName(path)}");
        return _serializer.Deserialize(File.ReadAllText(path));
    }

    /// <summary>The parts of a document a user would notice changing.</summary>
    /// <remarks>
    /// Not the whole serialization: saving stamps metadata.modified on purpose, so
    /// comparing full bytes across a save asserts that time did not pass.
    /// </remarks>
    private static string Content(AprDocument document)
    {
        static IEnumerable<string> Walk(Section s, string path) =>
            s.Prompts.Select(p => $"{path}/{s.Id}/{p.Id}|{p.Label}|{p.Response}|{p.Hints.ExpectedDataType}")
             .Concat(s.Sections.SelectMany(c => Walk(c, $"{path}/{s.Id}")));

        return string.Join("\n", document.Sections.SelectMany(s => Walk(s, string.Empty)));
    }

    private static void MustBeValid(AprDocument document, string what)
    {
        var result = new DocumentValidator().Validate(document);
        result.IsValid.Should().BeTrue(
            $"{what} must produce a valid document. Errors: " +
            string.Join(" | ", result.Errors.Select(e => $"{e.ErrorCode}: {e.Message}")));
    }

    // ── Authoring ────────────────────────────────────────────────────────────

    /// <summary>New template, build it, save it, open it again.</summary>
    [Fact]
    public async Task AuthorATemplateFromNothing_AndReopenIt()
    {
        _shell.NewTemplateCommand.Execute(null);
        _shell.HasDocument.Should().BeTrue("a new template opens a document");
        _session.Mode.Should().Be(DocumentMode.EditingTemplate);

        var section = _shell.Sections.Should().ContainSingle().Subject;
        section.Title = "Applicant";
        var prompt = section.PromptViewModels.Should().ContainSingle().Subject;
        prompt.Label = "Full name";
        prompt.ExpectedDataType = "text";
        prompt.HelpText = "As it appears on your passport.";

        var second = section.AddPrompt();
        second.Label = "Date of birth";
        second.ExpectedDataType = "date";

        var path = Path_("authored.aprt");
        await _files.SaveFileAsync(_session.CurrentDocument!, path);

        var reloaded = Reload(path);
        MustBeValid(reloaded, "authoring a template");
        reloaded.DocumentType.Should().Be(DocumentType.Template);

        var saved = reloaded.Sections.Should().ContainSingle().Subject;
        saved.Title.Should().Be("Applicant");
        saved.Prompts.Select(p => p.Label).Should().Equal("Full name", "Date of birth");
        saved.Prompts[0].Hints.HelpText.Should().Be("As it appears on your passport.");
        saved.Prompts[1].Hints.ExpectedDataType.Should().Be("date");
    }

    /// <summary>Add sections and prompts, undo the lot, redo the lot.</summary>
    /// <remarks>
    /// Undo is tested per command elsewhere. What is not is whether a whole session
    /// unwinds and rewinds cleanly, which is when people actually reach for it.
    /// </remarks>
    [Fact]
    public void UndoAndRedoAWholeAuthoringSession()
    {
        _shell.NewTemplateCommand.Execute(null);
        var before = _serializer.Serialize(_session.CurrentDocument!);

        var section = _shell.Sections[0];
        section.Title = "Employment";
        section.AddPrompt().Label = "Employer";
        section.AddPrompt().Label = "Role";
        var nested = section.AddNestedSection();
        nested.Title = "Previous employer";

        var after = _serializer.Serialize(_session.CurrentDocument!);
        after.Should().NotBe(before, "the session changed the document");

        while (_shell.UndoCommand.CanExecute(null)) _shell.UndoCommand.Execute(null);
        _serializer.Serialize(_session.CurrentDocument!).Should().Be(before,
            "undoing every step must land exactly where the session started");

        while (_shell.RedoCommand.CanExecute(null)) _shell.RedoCommand.Execute(null);
        _serializer.Serialize(_session.CurrentDocument!).Should().Be(after,
            "and redoing every step must land exactly where it ended");
    }

    /// <summary>Build a table, give it a column and rows, save, reopen.</summary>
    [Fact]
    public async Task BuildATable_FillItsCells_AndReopenIt()
    {
        _shell.NewTemplateCommand.Execute(null);
        var section = _shell.Sections[0];
        section.Title = "Line items";
        section.ConvertToDynamicTable();
        section.IsTableSection.Should().BeTrue();

        section.AddColumn();
        section.AddRow();
        section.Columns.Should().HaveCount(2);
        section.NestedSections.Should().HaveCount(2, "a starter row plus the one just added");

        foreach (var (row, index) in section.NestedSections.Select((r, i) => (r, i)))
        {
            foreach (var (cell, column) in row.PromptViewModels.Select((c, j) => (c, j)))
            {
                cell.Response = $"r{index}c{column}";
            }
        }

        var path = Path_("table.aprt");
        await _files.SaveFileAsync(_session.CurrentDocument!, path);

        var reloaded = Reload(path);
        MustBeValid(reloaded, "building a table");
        var table = reloaded.Sections.Should().ContainSingle().Subject;
        table.Kind.Should().Be("table");
        table.Sections.Should().HaveCount(2);
        // Every cell the user typed into must survive the round trip.
        table.Sections.SelectMany(r => r.Prompts).Select(p => p.Response)
            .Should().Equal("r0c0", "r0c1", "r1c0", "r1c1");
        table.Sections.SelectMany(r => r.Prompts).Select(p => p.Id)
            .Should().OnlyHaveUniqueItems("cells share the document's id namespace");
    }

    // ── Filling ──────────────────────────────────────────────────────────────

    /// <summary>Open a template to fill it, answer it, save it as a filled form.</summary>
    [Fact]
    public async Task FillATemplate_AndSaveItAsAFilledForm()
    {
        var templatePath = Path_("blank.aprt");
        File.WriteAllText(templatePath, _serializer.Serialize(new AprDocument
        {
            DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "Intake", TemplateId = "intake", TemplateVersion = "1.0" },
            Sections =
            [
                new Section
                {
                    Id = "s", Title = "About you",
                    Prompts =
                    [
                        new Prompt { Id = "name", Label = "Name" },
                        new Prompt { Id = "email", Label = "Email",
                            Hints = new PromptHints { ExpectedDataType = "email" } },
                    ],
                },
            ],
        }));

        var loaded = await _files.LoadFileAsync(templatePath);
        _session.Set(loaded!, templatePath);

        foreach (var (prompt, answer) in _shell.PromptViewModels
                     .Zip(new[] { "Ada Lovelace", "ada@example.com" }))
        {
            prompt.Response = answer;
        }

        // Filling a template produces a filled form, saved somewhere new.
        _session.CurrentDocument!.DocumentType = DocumentType.FilledForm;
        var filledPath = Path_("answered.aprf");
        await _files.SaveFileAsync(_session.CurrentDocument!, filledPath);

        var reloaded = Reload(filledPath);
        MustBeValid(reloaded, "filling a form");
        reloaded.DocumentType.Should().Be(DocumentType.FilledForm);
        reloaded.Sections[0].Prompts.Select(p => p.Response)
            .Should().Equal("Ada Lovelace", "ada@example.com");
        reloaded.Metadata.TemplateId.Should().Be("intake",
            "a filled form remembers which template it answers");
    }

    /// <summary>Launch-path equivalent for the largest public template: open it
    /// to fill, answer a field, save a filled copy, and reopen the answer.</summary>
    [Fact]
    public async Task FillSf86Template_SaveFilledCopy_AndReopenIt()
    {
        var source = RepositoryPath("examples", "sf-86-background-check.aprt");
        var filledPath = Path_("sf-86-filled.aprf");

        await _shell.OpenFromPath(source, openForFilling: true);

        _shell.IsEditMode.Should().BeFalse("--open is the form-filling entry point");
        _shell.ShowFullFillList.Should().BeTrue();
        _shell.PromptViewModels.Should().HaveCount(111);
        _shell.PromptViewModels.Single(p => p.Id == "prompt_investigation_type").Response =
            "Initial Investigation";

        _session.CurrentDocument!.DocumentType = DocumentType.FilledForm;
        await _files.SaveFileAsync(_session.CurrentDocument, filledPath);

        var reloaded = Reload(filledPath);
        MustBeValid(reloaded, "filling the SF-86 template");
        reloaded.DocumentType.Should().Be(DocumentType.FilledForm);
        reloaded.Metadata.TemplateId.Should().Be("sf-86-2024");
        reloaded.Sections.SelectMany(Flatten).Single(p => p.Id == "prompt_investigation_type").Response
            .Should().Be("Initial Investigation");

        static IEnumerable<Prompt> Flatten(Section s) =>
            s.Prompts.Concat(s.Sections.SelectMany(Flatten));
    }

    /// <summary>Free text is accepted on a typed field, all the way to disk.</summary>
    /// <remarks>
    /// The format's central promise, exercised as a journey rather than as a unit
    /// assertion: a hint suggests, it never restricts (specification 3.3).
    /// </remarks>
    [Fact]
    public async Task AnswerADateFieldInPlainEnglish_AndItSurvivesTheRoundTrip()
    {
        _shell.NewTemplateCommand.Execute(null);
        var prompt = _shell.Sections[0].PromptViewModels[0];
        prompt.Label = "When did it happen?";
        prompt.ExpectedDataType = "date";
        prompt.Response = "some time last spring, I think";

        var path = Path_("freetext.aprt");
        await _files.SaveFileAsync(_session.CurrentDocument!, path);

        var reloaded = Reload(path);
        MustBeValid(reloaded, "answering a date field in prose");
        reloaded.Sections[0].Prompts[0].Response.Should().Be("some time last spring, I think",
            "a type hint suggests an affordance; it never restricts what may be written");
    }

    /// <summary>Reopen a filled form, change an answer, save over it.</summary>
    [Fact]
    public async Task ReopenAFilledForm_ChangeAnAnswer_AndSaveOverIt()
    {
        var path = Path_("existing.aprf");
        File.WriteAllText(path, _serializer.Serialize(new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata { Title = "Claim", TemplateId = "c", TemplateVersion = "1.0" },
            Sections =
            [
                new Section
                {
                    Id = "s", Title = "Claim",
                    Prompts = [new Prompt { Id = "amount", Label = "Amount", Response = "100" }],
                },
            ],
        }));

        _session.Set((await _files.LoadFileAsync(path))!, path);
        _session.Mode.Should().Be(DocumentMode.FillingForm,
            "a filled form opens ready to fill, not to restructure");

        _shell.PromptViewModels.Single().Response = "250";
        await _files.SaveFileAsync(_session.CurrentDocument!, path);

        Reload(path).Sections[0].Prompts[0].Response.Should().Be("250");
    }

    // ── Multi-party ──────────────────────────────────────────────────────────

    /// <summary>Choose a role, fill your part, leave the rest.</summary>
    [Fact]
    public async Task FillOnlyYourOwnPartOfAMultiPartyForm()
    {
        var corpus = Path.Combine(
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..")),
            "tests", "Conformance", "v1", "valid", "roles.aprt");
        _session.Set((await _files.LoadFileAsync(corpus))!, corpus);

        _shell.HasRoles.Should().BeTrue();
        _shell.ActiveRoleChoice = _shell.AvailableRoles.Single(r => r.Id == "patient");

        foreach (var mine in _shell.PromptViewModels.Where(p => p.IsMine))
        {
            mine.Response = "answered";
        }

        var path = Path_("partly-filled.aprf");
        _session.CurrentDocument!.DocumentType = DocumentType.FilledForm;
        await _files.SaveFileAsync(_session.CurrentDocument!, path);

        var reloaded = Reload(path);
        MustBeValid(reloaded, "filling one party's share");

        var answered = reloaded.Sections.SelectMany(Flatten).Count(p => !string.IsNullOrEmpty(p.Response));
        answered.Should().Be(4, "the patient's three fields plus the unassigned consent question");

        static IEnumerable<Prompt> Flatten(Section s) =>
            s.Prompts.Concat(s.Sections.SelectMany(Flatten));
    }

    // ── Wizard ───────────────────────────────────────────────────────────────

    /// <summary>Walk a form section by section from first to last.</summary>
    [Fact]
    public void WalkAWholeFormThroughTheWizard()
    {
        _shell.NewTemplateCommand.Execute(null);
        foreach (var title in new[] { "Second", "Third" })
        {
            _shell.AddTopLevelSectionCommand.Execute(null);
            _shell.Sections[^1].Title = title;
        }
        _shell.Sections.Should().HaveCount(3);

        _shell.ToggleWizardModeCommand.Execute(null);
        _shell.IsWizardMode.Should().BeTrue();

        var visited = 1;
        while (_shell.WizardNextCommand.CanExecute(null))
        {
            _shell.WizardNextCommand.Execute(null);
            visited++;
        }

        visited.Should().Be(3, "the wizard must reach every section, then stop");

        var back = 0;
        while (_shell.WizardPreviousCommand.CanExecute(null))
        {
            _shell.WizardPreviousCommand.Execute(null);
            back++;
        }
        back.Should().Be(2, "and walk back to the first without running past it");
    }

    // ── Export ───────────────────────────────────────────────────────────────

    /// <summary>Fill a form, export it as a fillable PDF, and find the answers in the PDF.</summary>
    /// <remarks>
    /// The end of the road for most documents: a fillable export gets emailed and opened
    /// in Acrobat, and the .aprf is never seen again. Export was covered by a test that
    /// checked a file appeared; this checks the file contains what the person typed.
    /// </remarks>
    [Fact]
    public async Task FillAForm_ExportAFillablePdf_AndFindTheAnswersInIt()
    {
        _shell.NewTemplateCommand.Execute(null);
        var section = _shell.Sections[0];
        section.Title = "Claim";
        var first = section.PromptViewModels[0];
        first.Label = "Claimant";
        first.Response = "Ada Lovelace";
        var second = section.AddPrompt();
        second.Label = "Department";
        second.ExpectedDataType = "select";
        second.Response = "Engineering";

        var pdfPath = Path_("claim.pdf");
        _files.NextExportPath = pdfPath;

        await _shell.ExportPdfForm();

        File.Exists(pdfPath).Should().BeTrue("the export workflow must produce a file");
        var bytes = File.ReadAllBytes(pdfPath);
        var text = System.Text.Encoding.Latin1.GetString(bytes);

        text.Should().StartWith("%PDF", "and the file must actually be a PDF");
        text.Should().Contain("/AcroForm", "a fillable export carries a form");
        text.Should().Contain("/Widget", "with fields somebody can type into");
        bytes.Length.Should().BeGreaterThan(1000, "a form with two fields is not an empty page");
    }

    /// <summary>Exporting must not disturb the document being exported.</summary>
    /// <remarks>
    /// Asserted at the renderer already; asserted here as the user meets it, because
    /// exporting mid-session and then saving is an ordinary thing to do and a mutation
    /// would be written to their file.
    /// </remarks>
    [Fact]
    public async Task ExportingThenSaving_WritesWhatTheUserBuilt()
    {
        _shell.NewTemplateCommand.Execute(null);
        _shell.Sections[0].PromptViewModels[0].Response = "before export";
        _shell.Sections[0].PromptViewModels[0].Label = "Typed before exporting";
        var before = Content(_session.CurrentDocument!);

        _files.NextExportPath = Path_("side-effect.pdf");
        await _shell.ExportPdfForm();

        var path = Path_("after-export.aprt");
        await _files.SaveFileAsync(_session.CurrentDocument!, path);

        Content(Reload(path)).Should().Be(before,
            "exporting is a read of the document, and saving afterwards must write what " +
            "the user built rather than something the renderer left behind");
    }
}
