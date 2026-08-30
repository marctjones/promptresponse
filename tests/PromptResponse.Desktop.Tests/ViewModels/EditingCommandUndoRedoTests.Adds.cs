using AwesomeAssertions;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

public partial class EditingCommandUndoRedoTests
{
    [Fact]
    public void AddColumn_RedoAfterUndo_ReusesCapturedColumn_NotASynthFreshOne()
    {
        // Undo/redo restores the section snapshot: the meaningful invariant is
        // restoring the added column's data, rather than synthesizing a default.
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

        history.Undo();
        vm.NestedSections.Should().BeEmpty();
        history.Redo();
        vm.NestedSections.Should().ContainSingle();
        vm.NestedSections[0].Should().Be(child,
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
