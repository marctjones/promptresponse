using AwesomeAssertions;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

public partial class EditingCommandUndoRedoTests
{
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
        var fakeFactory = new PromptViewModelFactory(NewService());
        var stranger = new SectionViewModel(new() { Id = "stranger", Title = "Stranger" }, fakeFactory, depth: 0);

        shell.RemoveTopLevelSection(stranger);
        history.CanUndo.Should().BeFalse("removing a section that isn't in the shell must be a no-op");
    }

    [Fact]
    public void MoveTopLevelSection_RedoAfterUndo_ReappliesMove()
    {
        var (shell, doc, history) = NewShell(sectionCount: 3);
        shell.MoveTopLevelSection(0, 2);
        doc.Sections.Select(s => s.Id).Should().Equal("s1", "s2", "s0");
        history.Undo();
        doc.Sections.Select(s => s.Id).Should().Equal("s0", "s1", "s2");
        history.Redo();
        doc.Sections.Select(s => s.Id).Should().Equal(new[] { "s1", "s2", "s0" },
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
}
