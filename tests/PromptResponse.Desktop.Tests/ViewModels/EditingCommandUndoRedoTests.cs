using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Editing;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// Closes the coverage gap on the structural editing commands' Undo + Redo
/// paths. Scenario groups live in sibling partial files; this file keeps the
/// common public-ViewModel fixture together.
/// </summary>
public partial class EditingCommandUndoRedoTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static IProfileService NewService() => new ProfileService(new FixedProbe(), applyAffordanceDefaults: false);

    private static (SectionViewModel vm, EditHistory history) NewSection()
    {
        var history = new EditHistory();
        var factory = new PromptViewModelFactory(NewService(), history);
        var section = new Section { Id = "s1", Title = "S", Prompts = new List<Prompt>() };
        var vm = new SectionViewModel(section, factory, depth: 0, history: history);
        return (vm, history);
    }

    private static (MainShellViewModel shell, AprDocument doc, EditHistory history) NewShell(int sectionCount = 3)
    {
        var fs = Substitute.For<IFileService>();
        var dlg = Substitute.For<IDialogService>();
        var session = new DocumentSessionService();
        var profile = NewService();
        var factory = new PromptViewModelFactory(profile);
        var shell = new MainShellViewModel(fs, dlg, session, profile, factory);

        var doc = new AprDocument
        {
            Version = AprFormat.CurrentVersion,
            DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "T" },
            Sections = new List<Section>(),
        };
        for (var i = 0; i < sectionCount; i++)
        {
            doc.Sections.Add(new Section { Id = $"s{i}", Title = $"S{i}", Prompts = new List<Prompt>() });
        }
        session.Set(doc, filePath: null, dirty: false);
        return (shell, doc, shell.EditHistory);
    }

}
