using AwesomeAssertions;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

public partial class EditingCommandUndoRedoTests
{
    [Fact]
    public void RemoveNestedSection_Undo_ReinsertsAtOriginalIndex_WithItsPrompts()
    {
        var (vm, history) = NewSection();
        var c1 = vm.AddNestedSection(); c1.Title = "First";
        var c2 = vm.AddNestedSection(); c2.Title = "Middle"; c2.AddPrompt();
        var c2PromptCount = c2.PromptViewModels.Count;
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
        vm.ConvertToFixedTable(); vm.AddColumn(); vm.AddFixedRow(); history.Clear();
        var columnCountBefore = vm.Columns.Count;
        var rowCountBefore = vm.NestedSections.Count;

        vm.RemoveTableLayout();
        vm.IsTableSection.Should().BeFalse();
        history.Undo();
        vm.IsTableSection.Should().BeTrue();
        vm.Columns.Should().HaveCount(columnCountBefore, "undo must restore every column, not collapse the layout to a starter");
        vm.NestedSections.Should().HaveCount(rowCountBefore, "undo must restore every row sub-section");
        history.Redo();
        vm.IsTableSection.Should().BeFalse();
    }

    [Fact]
    public void RemoveColumn_Undo_RestoresColumn_AndCellPromptsInEveryRow()
    {
        var (vm, history) = NewSection();
        vm.ConvertToFixedTable(); vm.AddColumn(); vm.AddColumn(); vm.AddFixedRow(); history.Clear();
        var col2 = vm.Columns[1];
        var col2Label = col2.Label;

        vm.RemoveColumn(col2);
        vm.Columns.Should().HaveCount(2);
        foreach (var rowVm in vm.NestedSections) rowVm.PromptViewModels.Should().HaveCount(2);
        history.Undo();
        vm.Columns.Should().HaveCount(3);
        vm.Columns[1].Label.Should().Be(col2Label, "undo must reinsert the same column at its original index");
        foreach (var rowVm in vm.NestedSections) rowVm.PromptViewModels.Should().HaveCount(3, "every row's missing cell prompt must come back when the column does");
        history.Redo();
        vm.Columns.Should().HaveCount(2);
    }

    [Fact]
    public void RemoveFixedRow_Undo_RestoresRowAtOriginalIndex_WithCells()
    {
        var (vm, history) = NewSection();
        vm.ConvertToFixedTable(); vm.AddColumn(); vm.AddFixedRow(); vm.AddFixedRow(); history.Clear();
        var middleRow = vm.NestedSections[1];
        var middleRowTitle = middleRow.Title;

        vm.RemoveFixedRow(middleRow);
        vm.NestedSections.Should().HaveCount(2);
        vm.NestedSections.Should().NotContain(middleRow);
        history.Undo();
        vm.NestedSections.Should().HaveCount(3);
        vm.NestedSections[1].Title.Should().Be(middleRowTitle, "undo must reinsert the row at its original VM index, not append");
        vm.NestedSections[1].PromptViewModels.Should().HaveCount(2, "row's cell prompts come back too");
        history.Redo();
        vm.NestedSections.Should().HaveCount(2);
    }
}
