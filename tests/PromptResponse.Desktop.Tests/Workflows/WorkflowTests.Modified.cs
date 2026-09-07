using AwesomeAssertions;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.Workflows;

/// <summary>
/// `metadata.modified` says when the document changed, and a save is not a change.
/// </summary>
/// <remarks>
/// It used to be stamped in `AprDocumentPersistence.SaveAsync`, and not in
/// `SaveStreamAsync` beside it — so the same Save updated it or did not depending on
/// whether the file had been opened in this session. Stamping on save also moved the
/// semantic digest of a document nobody edited, which stops every attestation over it
/// resolving; `OpenAndSaveKeepsTheDigestTests` holds that.
///
/// The session is the only thing that knows the difference, so the stamp lives there.
/// </remarks>
public class ModifiedIsStampedByEditingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public ModifiedIsStampedByEditingTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    private static AprDocument Template() => new()
    {
        Version = AprFormat.CurrentVersion,
        Metadata = new Metadata { Title = "Stamped" },
        Sections = [new Section { Id = "s", Title = "S",
            Prompts = [new Prompt { Id = "p", Label = "P" }] }],
    };

    [Fact]
    public async Task SaveEditedDocument_StampsModified()
    {
        var harness = new SaveHarness(_dir);
        harness.Session.Set(Template(), Path.Combine(_dir, "edited.aprt"), dirty: true);
        var before = harness.Session.CurrentDocument!.Metadata.Modified;

        await harness.Shell.Save();

        harness.Session.CurrentDocument!.Metadata.Modified.Should().NotBe(before,
            "the document was edited, and that is what modified records");
    }

    [Fact]
    public async Task SaveUnchangedDocument_LeavesModifiedAlone()
    {
        var harness = new SaveHarness(_dir);
        harness.Session.Set(Template(), Path.Combine(_dir, "clean.aprt"), dirty: false);
        var before = harness.Session.CurrentDocument!.Metadata.Modified;

        await harness.Shell.Save();

        harness.Session.CurrentDocument!.Metadata.Modified.Should().Be(before,
            "opening a document and saving it is not editing it");
    }
}
/// <summary>The shell over a real file service, writing into a scratch directory.</summary>
internal sealed class SaveHarness
{
    internal SaveHarness(string directory)
    {
        Session = new DocumentSessionService();
        var profile = new ProfileService(new NoPreferences(), applyAffordanceDefaults: false);
        Shell = new MainShellViewModel(new FileService(new AprJsonSerializer()),
            NSubstitute.Substitute.For<IDialogService>(), Session, profile,
            new PromptViewModelFactory(profile));
    }

    internal DocumentSessionService Session { get; }
    internal MainShellViewModel Shell { get; }

    private sealed class NoPreferences : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }
}
