using AwesomeAssertions;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

public partial class EditingCommandUndoRedoTests
{
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
        vm.ConvertToFixedTable(); vm.AddColumn(); vm.AddColumn(); history.Clear();
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
        vm.ConvertToFixedTable(); vm.AddFixedRow(); vm.AddFixedRow(); history.Clear();
        var titlesBefore = vm.NestedSections.Select(r => r.Title).ToArray();

        vm.MoveFixedRow(0, 2);
        vm.NestedSections.Select(r => r.Title).Should().Equal(titlesBefore[1], titlesBefore[2], titlesBefore[0]);
        history.Undo();
        vm.NestedSections.Select(r => r.Title).Should().Equal(titlesBefore);
        history.Redo();
        vm.NestedSections.Select(r => r.Title).Should().Equal(titlesBefore[1], titlesBefore[2], titlesBefore[0]);
    }
}
