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
/// Tests for the typed Move methods that drag-drop reorder dispatches to.
/// These cover the model + VM tree mutations that happen when a user drags
/// an item from one position to another. Headless drag-drop synthesis isn't
/// supported by the Avalonia harness, so the drag UX itself is tested
/// implicitly via these underlying VM operations.
/// </summary>
public class ReorderTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static IProfileService NewService() => new ProfileService(new FixedProbe(), applyAffordanceDefaults: false);

    private static (SectionViewModel vm, Section model, EditHistory history) NewSectionWithPrompts(int promptCount)
    {
        var history = new EditHistory();
        var factory = new PromptViewModelFactory(NewService(), history);
        var section = new Section { Id = "s1", Title = "S", Prompts = new List<Prompt>() };
        var vm = new SectionViewModel(section, factory, depth: 0, history: history);
        for (var i = 0; i < promptCount; i++)
        {
            var p = vm.AddPrompt();
            p.Label = $"Prompt {i + 1}";
        }
        history.Clear(); // Reset so reorder tests start with an empty undo stack.
        return (vm, section, history);
    }

    [Fact]
    public void MovePrompt_FromTopToBottom_ReordersBothModelAndVMTree()
    {
        var (vm, model, _) = NewSectionWithPrompts(3);

        vm.MovePrompt(0, 2);

        vm.PromptViewModels.Select(p => p.Label).Should().Equal("Prompt 2", "Prompt 3", "Prompt 1");
        // Model order must match the VM tree so save persists the reorder.
        model.Prompts.Select(p => p.Label).Should().Equal(new[] { "Prompt 2", "Prompt 3", "Prompt 1" });
    }

    [Fact]
    public void MovePrompt_BetweenMiddlePositions_Works()
    {
        var (vm, model, _) = NewSectionWithPrompts(4);

        vm.MovePrompt(2, 1);

        vm.PromptViewModels.Select(p => p.Label).Should().Equal("Prompt 1", "Prompt 3", "Prompt 2", "Prompt 4");
    }

    [Fact]
    public void MovePrompt_Undo_RestoresOriginalOrder()
    {
        var (vm, _, history) = NewSectionWithPrompts(3);

        vm.MovePrompt(0, 2);

        history.CanUndo.Should().BeTrue();
        history.Undo();
        vm.PromptViewModels.Select(p => p.Label).Should().Equal("Prompt 1", "Prompt 2", "Prompt 3");
    }

    [Fact]
    public void MovePrompt_OutOfRangeIndex_IsIgnored()
    {
        var (vm, _, history) = NewSectionWithPrompts(3);

        vm.MovePrompt(0, 99);
        vm.MovePrompt(-1, 1);
        vm.MovePrompt(5, 0);

        history.CanUndo.Should().BeFalse("invalid moves don't push undo entries");
        vm.PromptViewModels.Select(p => p.Label).Should().Equal("Prompt 1", "Prompt 2", "Prompt 3");
    }

    [Fact]
    public void MoveNestedSection_ReordersChildren()
    {
        var history = new EditHistory();
        var factory = new PromptViewModelFactory(NewService(), history);
        var section = new Section { Id = "root", Title = "Root" };
        var vm = new SectionViewModel(section, factory, depth: 0, history: history);
        var c1 = vm.AddNestedSection(); c1.Title = "Alpha";
        var c2 = vm.AddNestedSection(); c2.Title = "Beta";
        var c3 = vm.AddNestedSection(); c3.Title = "Gamma";
        history.Clear();

        vm.MoveNestedSection(0, 2);

        vm.NestedSections.Select(s => s.Title).Should().Equal("Beta", "Gamma", "Alpha");
        section.Sections.Select(s => s.Title).Should().Equal("Beta", "Gamma", "Alpha");
    }

    [Fact]
    public void MoveColumn_ReordersWithoutLosingCellPrompts()
    {
        var history = new EditHistory();
        var factory = new PromptViewModelFactory(NewService(), history);
        var section = new Section { Id = "tbl", Title = "T" };
        var vm = new SectionViewModel(section, factory, depth: 0, history: history);
        vm.ConvertToFixedTable();
        vm.AddColumn(); // 2 columns
        vm.AddColumn(); // 3 columns
        vm.AddFixedRow(); // 2 rows × 3 cols = 6 cells
        history.Clear();

        var labelsBefore = vm.Columns.Select(c => c.Label).ToArray();

        vm.MoveColumn(0, 2);

        vm.Columns.Select(c => c.Label).Should().Equal(labelsBefore[1], labelsBefore[2], labelsBefore[0]);
        // Cell prompts are keyed by id; their underlying prompts haven't moved.
        // What changed is the visual column order. Verify no cells were lost.
        foreach (var rowVm in vm.NestedSections)
        {
            // No cells lost during column reorder.
            rowVm.PromptViewModels.Should().HaveCount(3);
        }
    }

    [Fact]
    public void MoveFixedRow_ReordersBothLayoutAndRowSubSections()
    {
        var history = new EditHistory();
        var factory = new PromptViewModelFactory(NewService(), history);
        var section = new Section { Id = "tbl", Title = "T" };
        var vm = new SectionViewModel(section, factory, depth: 0, history: history);
        vm.ConvertToFixedTable();
        vm.AddFixedRow(); // 2 rows now
        vm.AddFixedRow(); // 3 rows now
        history.Clear();

        var titlesBefore = vm.NestedSections.Select(r => r.Title).ToArray();

        vm.MoveFixedRow(0, 2);

        vm.NestedSections.Select(r => r.Title).Should().Equal(titlesBefore[1], titlesBefore[2], titlesBefore[0]);
        // The rows ARE the child sections: there is no second list that could disagree.
        section.Sections.Select(r => r.Title).Should().Equal(titlesBefore[1], titlesBefore[2], titlesBefore[0]);
    }

    [Fact]
    public void MoveTopLevelSection_ReordersDocumentSections_AndUndoRestores()
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
            Sections = new List<Section>
            {
                new() { Id = "a", Title = "A", Prompts = new List<Prompt> { new() { Id = "a.p", Label = "ap" } } },
                new() { Id = "b", Title = "B", Prompts = new List<Prompt> { new() { Id = "b.p", Label = "bp" } } },
                new() { Id = "c", Title = "C", Prompts = new List<Prompt> { new() { Id = "c.p", Label = "cp" } } },
            },
        };
        session.Set(doc, filePath: null, dirty: false);

        shell.MoveTopLevelSection(0, 2);

        doc.Sections.Select(s => s.Title).Should().Equal("B", "C", "A");
        shell.Sections.Select(s => s.Title).Should().Equal("B", "C", "A");

        shell.EditHistory.Undo();
        doc.Sections.Select(s => s.Title).Should().Equal("A", "B", "C");
        shell.Sections.Select(s => s.Title).Should().Equal("A", "B", "C");
    }
}
