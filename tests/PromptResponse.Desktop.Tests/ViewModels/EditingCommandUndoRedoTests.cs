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
/// paths. The earlier UndoRedoTests / ReorderTests covered the Execute path;
/// these exercise the inverse + redo cycle on every command — important
/// because each command captures inverse state on first Execute and that
/// captured state is what Redo replays.
///
/// Driven through the public ViewModel API rather than the internal command
/// classes themselves: that's how the editor uses them, and it ensures the
/// command is wired up correctly all the way through the VM.
/// </summary>
public class EditingCommandUndoRedoTests
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

    // ── Top-level section commands (shell-level) ──

    [Fact]
    public void AddTopLevelSection_ExecuteUndoRedo_RoundTripsCleanly()
    {
        var (shell, doc, history) = NewShell(sectionCount: 2);
        var beforeCount = doc.Sections.Count;

        shell.AddTopLevelSection();

        doc.Sections.Should().HaveCount(beforeCount + 1);
        shell.Sections.Should().HaveCount(beforeCount + 1);
        var added = shell.Sections.Last();

        history.Undo();
        doc.Sections.Should().HaveCount(beforeCount);
        shell.Sections.Should().NotContain(added,
            "undo of AddTopLevelSection must remove the newly-inserted section from both the doc and the VM tree");

        history.Redo();
        doc.Sections.Should().HaveCount(beforeCount + 1);
        shell.Sections.Last().Should().Be(added,
            "redo must reinsert the SAME VM instance so anything that was holding a reference (drag handle, focus) keeps working");
    }

    [Fact]
    public void RemoveTopLevelSection_Undo_ReinsertsAtOriginalIndex()
    {
        var (shell, doc, history) = NewShell(sectionCount: 3);
        var middle = shell.Sections[1];
        var middleId = middle.Id;

        shell.RemoveTopLevelSection(middle);

        doc.Sections.Should().HaveCount(2);
        shell.Sections.Should().NotContain(middle);

        history.Undo();
        doc.Sections.Should().HaveCount(3);
        doc.Sections[1].Id.Should().Be(middleId, "undo must reinsert at the original index, not append");
        shell.Sections[1].Id.Should().Be(middleId);

        history.Redo();
        doc.Sections.Should().HaveCount(2);
        doc.Sections.Should().NotContain(s => s.Id == middleId);
    }

    [Fact]
    public void RemoveTopLevelSection_Null_IsIgnored_NoUndoEntry()
    {
        var (shell, _, history) = NewShell(sectionCount: 1);

        shell.RemoveTopLevelSection(null);

        history.CanUndo.Should().BeFalse("a null target must not push an undo entry");
    }

    [Fact]
    public void RemoveTopLevelSection_NotInList_IsIgnored()
    {
        var (shell, _, history) = NewShell(sectionCount: 1);
        // Build an unrelated SectionViewModel that the shell doesn't know about.
        var fakeFactory = new PromptViewModelFactory(NewService());
        var stranger = new SectionViewModel(
            new Section { Id = "stranger", Title = "Stranger" }, fakeFactory, depth: 0);

        shell.RemoveTopLevelSection(stranger);

        history.CanUndo.Should().BeFalse("removing a section that isn't in the shell must be a no-op");
    }

    [Fact]
    public void MoveTopLevelSection_RedoAfterUndo_ReappliesMove()
    {
        var (shell, doc, history) = NewShell(sectionCount: 3);

        shell.MoveTopLevelSection(0, 2);
        doc.Sections.Select(s => s.Id).Should().Equal(new[] { "s1", "s2", "s0" });

        history.Undo();
        doc.Sections.Select(s => s.Id).Should().Equal(new[] { "s0", "s1", "s2" });

        history.Redo();
        doc.Sections.Select(s => s.Id).Should().Equal(
            new[] { "s1", "s2", "s0" },
            "redo must reapply the same move, not just leave the previous state");
    }

    [Fact]
    public void MoveTopLevelSection_SameIndex_NoOp_NoUndoEntry()
    {
        var (shell, _, history) = NewShell(sectionCount: 3);

        shell.MoveTopLevelSection(1, 1);

        history.CanUndo.Should().BeFalse("from == to is a no-op and must not pollute the undo stack");
    }

    [Fact]
    public void MoveTopLevelSection_OutOfRange_IsIgnored_NoUndoEntry()
    {
        var (shell, _, history) = NewShell(sectionCount: 3);

        shell.MoveTopLevelSection(-1, 1);
        shell.MoveTopLevelSection(0, 99);
        shell.MoveTopLevelSection(99, 0);

        history.CanUndo.Should().BeFalse("out-of-range indices must not push undo entries");
    }

    // ── Section-level structural commands ──

    [Fact]
    public void RemoveNestedSection_Undo_ReinsertsAtOriginalIndex_WithItsPrompts()
    {
        var (vm, history) = NewSection();
        var c1 = vm.AddNestedSection(); c1.Title = "First";
        var c2 = vm.AddNestedSection(); c2.Title = "Middle"; c2.AddPrompt();
        var c2PromptCount = c2.PromptViewModels.Count;   // starter prompt plus the added one
        var c3 = vm.AddNestedSection(); c3.Title = "Last";
        history.Clear();

        vm.RemoveNestedSection(c2);

        vm.NestedSections.Should().HaveCount(2);
        vm.NestedSections.Should().NotContain(c2);

        history.Undo();
        vm.NestedSections.IndexOf(c2).Should().Be(1, "undo must reinsert at the original index");
        c2.PromptViewModels.Should().HaveCount(c2PromptCount, "the prompts that lived inside the removed section must come back");

        history.Redo();
        vm.NestedSections.Should().NotContain(c2);
    }

    [Fact]
    public void RemoveTableLayout_Undo_RestoresLayout_RowsAndCellPrompts()
    {
        var (vm, history) = NewSection();
        vm.ConvertToFixedTable();
        vm.AddColumn();          // 2 columns
        vm.AddFixedRow();        // 2 rows × 2 cols = 4 cells
        history.Clear();

        var columnCountBefore = vm.Columns.Count;
        var rowCountBefore = vm.NestedSections.Count;

        vm.RemoveTableLayout();

        vm.IsTableSection.Should().BeFalse();

        history.Undo();

        vm.IsTableSection.Should().BeTrue();
        vm.Columns.Should().HaveCount(columnCountBefore,
            "undo must restore every column, not collapse the layout to a starter");
        vm.NestedSections.Should().HaveCount(rowCountBefore,
            "undo must restore every row sub-section");

        history.Redo();
        vm.IsTableSection.Should().BeFalse();
    }

    [Fact]
    public void RemoveColumn_Undo_RestoresColumn_AndCellPromptsInEveryRow()
    {
        var (vm, history) = NewSection();
        vm.ConvertToFixedTable();
        vm.AddColumn();          // 2 columns
        vm.AddColumn();          // 3 columns
        vm.AddFixedRow();        // 2 rows × 3 cols = 6 cells
        history.Clear();

        var col2 = vm.Columns[1];
        var col2Label = col2.Label;

        vm.RemoveColumn(col2);

        vm.Columns.Should().HaveCount(2);
        foreach (var rowVm in vm.NestedSections)
            rowVm.PromptViewModels.Should().HaveCount(2);

        history.Undo();

        vm.Columns.Should().HaveCount(3);
        vm.Columns[1].Label.Should().Be(col2Label, "undo must reinsert the same column at its original index");
        foreach (var rowVm in vm.NestedSections)
            rowVm.PromptViewModels.Should().HaveCount(3,
                "every row's missing cell prompt must come back when the column does");

        history.Redo();
        vm.Columns.Should().HaveCount(2);
    }

    [Fact]
    public void RemoveFixedRow_Undo_RestoresRowAtOriginalIndex_WithCells()
    {
        var (vm, history) = NewSection();
        vm.ConvertToFixedTable();
        vm.AddColumn();          // 2 columns
        vm.AddFixedRow();        // row 2
        vm.AddFixedRow();        // row 3 — 3 rows total, each with 2 cells
        history.Clear();

        var middleRow = vm.NestedSections[1];
        var middleRowTitle = middleRow.Title;

        vm.RemoveFixedRow(middleRow);

        vm.NestedSections.Should().HaveCount(2);
        vm.NestedSections.Should().NotContain(middleRow);

        history.Undo();
        vm.NestedSections.Should().HaveCount(3);
        vm.NestedSections[1].Title.Should().Be(middleRowTitle,
            "undo must reinsert the row at its original VM index, not append");
        vm.NestedSections[1].PromptViewModels.Should().HaveCount(2, "row's cell prompts come back too");

        history.Redo();
        vm.NestedSections.Should().HaveCount(2);
    }

    // ── Move commands: redo paths ──

    [Fact]
    public void MovePrompt_RedoAfterUndo_ReappliesMove()
    {
        var (vm, history) = NewSection();
        var p1 = vm.AddPrompt(); p1.Label = "P1";
        var p2 = vm.AddPrompt(); p2.Label = "P2";
        var p3 = vm.AddPrompt(); p3.Label = "P3";
        history.Clear();

        vm.MovePrompt(0, 2);
        vm.PromptViewModels.Select(p => p.Label).Should().Equal("P2", "P3", "P1");

        history.Undo();
        vm.PromptViewModels.Select(p => p.Label).Should().Equal("P1", "P2", "P3");

        history.Redo();
        vm.PromptViewModels.Select(p => p.Label).Should().Equal("P2", "P3", "P1");
    }

    [Fact]
    public void MoveNestedSection_RedoAfterUndo_ReappliesMove()
    {
        var (vm, history) = NewSection();
        var c1 = vm.AddNestedSection(); c1.Title = "Alpha";
        var c2 = vm.AddNestedSection(); c2.Title = "Beta";
        var c3 = vm.AddNestedSection(); c3.Title = "Gamma";
        history.Clear();

        vm.MoveNestedSection(0, 2);
        vm.NestedSections.Select(s => s.Title).Should().Equal("Beta", "Gamma", "Alpha");

        history.Undo();
        vm.NestedSections.Select(s => s.Title).Should().Equal("Alpha", "Beta", "Gamma");

        history.Redo();
        vm.NestedSections.Select(s => s.Title).Should().Equal("Beta", "Gamma", "Alpha");
    }

    [Fact]
    public void MoveColumn_RedoAfterUndo_ReappliesMove()
    {
        var (vm, history) = NewSection();
        vm.ConvertToFixedTable();
        vm.AddColumn();
        vm.AddColumn();
        history.Clear();

        var labelsBefore = vm.Columns.Select(c => c.Label).ToArray();

        vm.MoveColumn(0, 2);
        vm.Columns.Select(c => c.Label).Should().Equal(labelsBefore[1], labelsBefore[2], labelsBefore[0]);

        history.Undo();
        vm.Columns.Select(c => c.Label).Should().Equal(labelsBefore);

        history.Redo();
        vm.Columns.Select(c => c.Label).Should().Equal(labelsBefore[1], labelsBefore[2], labelsBefore[0]);
    }

    [Fact]
    public void MoveFixedRow_RedoAfterUndo_ReappliesMove()
    {
        var (vm, history) = NewSection();
        vm.ConvertToFixedTable();
        vm.AddFixedRow();
        vm.AddFixedRow();
        history.Clear();

        var titlesBefore = vm.NestedSections.Select(r => r.Title).ToArray();

        vm.MoveFixedRow(0, 2);
        vm.NestedSections.Select(r => r.Title).Should().Equal(titlesBefore[1], titlesBefore[2], titlesBefore[0]);

        history.Undo();
        vm.NestedSections.Select(r => r.Title).Should().Equal(titlesBefore);

        history.Redo();
        vm.NestedSections.Select(r => r.Title).Should().Equal(titlesBefore[1], titlesBefore[2], titlesBefore[0]);
    }

    // ── Add commands: redo paths exercise the captured-state restore branch ──

    [Fact]
    public void AddColumn_RedoAfterUndo_ReusesCapturedColumn_NotASynthFreshOne()
    {
        // Undo/redo is snapshot-based: the section tree is restored, so a redo brings
        // back the column's data rather than the identical view-model object. What
        // matters is that redo restores the column that was added, not a fresh default.
        var (vm, history) = NewSection();
        vm.ConvertToFixedTable();
        history.Clear();

        vm.AddColumn();
        var columnCount = vm.Columns.Count;
        var labelAfterAdd = vm.Columns.Last().Label;

        history.Undo();
        history.Redo();

        vm.Columns.Should().HaveCount(columnCount, "redo must restore the added column");
        vm.Columns.Last().Label.Should().Be(labelAfterAdd,
            "redo must restore the column that was added, not synthesize a fresh default");
    }

    [Fact]
    public void AddFixedRow_RedoAfterUndo_ReusesCapturedRow()
    {
        var (vm, history) = NewSection();
        vm.ConvertToFixedTable();
        history.Clear();

        vm.AddFixedRow();
        var rowCount = vm.NestedSections.Count;
        var rowIdAfterAdd = vm.NestedSections.Last().Id;

        history.Undo();
        history.Redo();

        vm.NestedSections.Should().HaveCount(rowCount, "redo must restore the added row");
        vm.NestedSections.Last().Id.Should().Be(rowIdAfterAdd,
            "redo must restore the row that was added, not synthesize a fresh one");
    }

    [Fact]
    public void AddNestedSection_RedoAfterUndo_RestoresChild()
    {
        var (vm, history) = NewSection();
        history.Clear();

        var child = vm.AddNestedSection();
        var childRef = child;

        history.Undo();
        vm.NestedSections.Should().BeEmpty();

        history.Redo();
        vm.NestedSections.Should().ContainSingle();
        vm.NestedSections[0].Should().Be(childRef,
            "redo must reattach the same VM instance so any external references stay valid");
    }

    [Fact]
    public void AddPrompt_RedoAfterUndo_RestoresPromptAtSameIndex()
    {
        var (vm, history) = NewSection();
        vm.AddPrompt();
        history.Clear();

        var p2 = vm.AddPrompt();

        history.Undo();
        vm.PromptViewModels.Should().ContainSingle("the second add must be undone, leaving only the first");

        history.Redo();
        vm.PromptViewModels.Should().HaveCount(2);
        vm.PromptViewModels[1].Should().Be(p2,
            "redo must reattach the same prompt VM at its original position");
    }
}
