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
public partial class WorkflowTests : IDisposable
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

}
