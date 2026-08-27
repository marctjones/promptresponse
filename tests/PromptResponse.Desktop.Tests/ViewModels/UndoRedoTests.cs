using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Editing;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// Tests for the editor's undo/redo history. Every editor mutation routed
/// through an <see cref="EditHistory"/> must be reversible (model + VM tree
/// both restored), and consecutive same-property keystrokes must merge into a
/// single undo step so the user doesn't have to Ctrl+Z 20 times to undo a
/// rename.
/// </summary>
public class UndoRedoTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static IProfileService NewService() => new ProfileService(new FixedProbe(), applyAffordanceDefaults: false);

    private static (SectionViewModel vm, Section model, EditHistory history, List<PromptViewModelBase> added, List<PromptViewModelBase> removed)
        NewSection()
    {
        var added = new List<PromptViewModelBase>();
        var removed = new List<PromptViewModelBase>();
        var history = new EditHistory();
        var factory = new PromptViewModelFactory(NewService(), history);
        var section = new Section { Id = "s1", Title = "Original title", Prompts = new List<Prompt>() };
        var vm = new SectionViewModel(section, factory, depth: 0,
            onPromptAdded: added.Add, onPromptRemoved: removed.Add, history: history);
        return (vm, section, history, added, removed);
    }

    // ── Property edits ──

    [Fact]
    public void TitleEdit_RecordsCommand_UndoRestores_RedoReapplies()
    {
        var (vm, model, history, _, _) = NewSection();

        vm.Title = "Renamed";

        history.CanUndo.Should().BeTrue();
        history.CanRedo.Should().BeFalse();

        history.Undo();
        vm.Title.Should().Be("Original title");
        model.Title.Should().Be("Original title", "undo must restore the model, not just the VM");
        history.CanUndo.Should().BeFalse();
        history.CanRedo.Should().BeTrue();

        history.Redo();
        vm.Title.Should().Be("Renamed");
        model.Title.Should().Be("Renamed");
    }

    [Fact]
    public void ConsecutiveTitleKeystrokes_MergeIntoSingleUndoStep()
    {
        // Typing "P → Pe → Per → Pers" on the same VM within the merge window
        // must collapse into one undo step that restores the original title.
        var (vm, _, history, _, _) = NewSection();

        vm.Title = "P";
        vm.Title = "Pe";
        vm.Title = "Per";
        vm.Title = "Pers";

        // One undo should fully restore.
        history.Undo();
        vm.Title.Should().Be("Original title",
            "consecutive same-property edits within the merge window must collapse to one undo step");
        history.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void EditsAcrossDifferentTargets_DoNotMerge()
    {
        var (vm1, _, history, _, _) = NewSection();
        // Add a second target — a prompt — and edit BOTH to ensure the merge
        // policy is per-target, not global.
        var prompt = vm1.AddPrompt();
        vm1.Title = "Renamed section";
        prompt.Label = "Renamed prompt";

        history.Undo(); // undo prompt label rename
        prompt.Label.Should().Be("New prompt", "label revert");
        vm1.Title.Should().Be("Renamed section", "section title still renamed");

        history.Undo(); // undo section title rename
        vm1.Title.Should().Be("Original title");
    }

    [Fact]
    public void PromptHintEdits_AreUndoable()
    {
        var (vm, _, history, _, _) = NewSection();
        var p = vm.AddPrompt();

        p.Placeholder = "type here";
        p.HelpText = "be brief";
        p.ExpectedDataType = "email";

        history.Undo(); p.ExpectedDataType.Should().Be("text", "default type restored");
        history.Undo(); p.HelpText.Should().BeNull();
        history.Undo(); p.Placeholder.Should().BeNull();
    }

    // ── Structural mutations ──

    [Fact]
    public void AddPrompt_Undo_RemovesPromptAndDetachesFromHost()
    {
        var (vm, model, history, added, removed) = NewSection();

        var p = vm.AddPrompt();

        added.Should().Contain(p);
        history.Undo();
        vm.PromptViewModels.Should().NotContain(p);
        model.Prompts.Should().BeEmpty();
        removed.Should().Contain(p, "host must hear about every prompt removed via undo");

        history.Redo();
        vm.PromptViewModels.Should().Contain(p);
        model.Prompts.Should().HaveCount(1);
    }

    [Fact]
    public void RemovePrompt_Undo_ReinsertsAtOriginalPosition()
    {
        var (vm, _, history, _, _) = NewSection();
        var p1 = vm.AddPrompt();
        var p2 = vm.AddPrompt();
        var p3 = vm.AddPrompt();

        vm.RemovePrompt(p2);

        history.Undo(); // undo the removal
        vm.PromptViewModels.IndexOf(p2).Should().Be(1,
            "undo of a middle removal must restore the prompt at its original index");
        vm.PromptViewModels[0].Should().Be(p1);
        vm.PromptViewModels[2].Should().Be(p3);
    }

    [Fact]
    public void AddNestedSection_Undo_RemovesAndDetachesAllPromptsRecursively()
    {
        var (vm, _, history, added, removed) = NewSection();

        var child = vm.AddNestedSection();
        var grandchild = child.AddNestedSection();
        grandchild.AddPrompt();
        grandchild.AddPrompt();
        // A new section arrives with a starter prompt, so the count includes those too.
        var attachedByAdds = added.Count;

        attachedByAdds.Should().BeGreaterThanOrEqualTo(2);
        var addedBeforeUndo = added.Count;
        var removedBeforeUndo = removed.Count;

        // Need to undo each: 2 prompt-add + nested + nested + (4 events) — let's
        // just undo all the way back.
        while (history.CanUndo) history.Undo();

        vm.NestedSections.Should().BeEmpty();
        // Every prompt that was added should also have been removed.
        (removed.Count - removedBeforeUndo).Should().Be(addedBeforeUndo);
    }

    [Fact]
    public void ConvertToFixedTable_Undo_RestoresOriginalRegularSectionWithItsPrompts()
    {
        var (vm, model, history, _, _) = NewSection();
        var p1 = vm.AddPrompt();
        var p2 = vm.AddPrompt();

        vm.ConvertToFixedTable();
        vm.IsTableSection.Should().BeTrue();

        history.Undo();

        vm.IsTableSection.Should().BeFalse();
        model.Kind.Should().BeNull();
        // The prompts that were on the section before conversion must come back.
        model.Prompts.Should().HaveCount(2);
        // We don't guarantee VM identity round-trip on table-snapshot restore,
        // but the model should be intact.
    }

    [Fact]
    public void AddColumn_Undo_RemovesColumnAndItsCellPrompts()
    {
        var (vm, _, history, _, _) = NewSection();
        vm.ConvertToFixedTable(); // 1 column, 1 row → 1 cell prompt
        vm.AddColumn();           // 2 columns, 1 row → 2 cell prompts

        vm.Columns.Should().HaveCount(2);
        vm.NestedSections[0].PromptViewModels.Should().HaveCount(2);

        history.Undo();

        vm.Columns.Should().HaveCount(1);
        vm.NestedSections[0].PromptViewModels.Should().HaveCount(1);
    }

    [Fact]
    public void AddFixedRow_Undo_RemovesRowAndItsCellPrompts()
    {
        var (vm, _, history, _, _) = NewSection();
        vm.ConvertToFixedTable();
        vm.NestedSections.Should().HaveCount(1, "starter row");

        vm.AddFixedRow();
        vm.NestedSections.Should().HaveCount(2);

        history.Undo();

        vm.NestedSections.Should().HaveCount(1, "undoing the add-row leaves only the starter row");
    }

    // ── History bookkeeping ──

    [Fact]
    public void NewExecute_AfterUndo_DropsRedoStack()
    {
        var (vm, _, history, _, _) = NewSection();

        vm.Title = "First rename";
        history.Undo();
        history.CanRedo.Should().BeTrue();

        vm.Title = "Different rename";
        history.CanRedo.Should().BeFalse(
            "the standard editor convention is that branching on undo drops the redo stack");
    }

    [Fact]
    public void Clear_DropsBothStacks()
    {
        var (vm, _, history, _, _) = NewSection();
        vm.Title = "Renamed";
        history.Undo();
        history.CanUndo.Should().BeFalse();
        history.CanRedo.Should().BeTrue();

        history.Clear();

        history.CanUndo.Should().BeFalse();
        history.CanRedo.Should().BeFalse();
    }
}
