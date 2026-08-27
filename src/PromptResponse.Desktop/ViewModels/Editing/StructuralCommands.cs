using PromptResponse.Core.Models;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.ViewModels.Editing;

/// <summary>
/// Structural editor commands. Each captures enough inverse state at
/// construction time that Undo can faithfully reverse Execute, including
/// position information so removed children come back at their original
/// index. Calls into <c>SectionViewModel.Apply…</c> internal helpers — the
/// raw mutations live with the VM, the commands just sequence them.
/// </summary>
internal static class StructuralCommands { }

internal sealed class AddPromptCommand : IEditCommand
{
    private readonly SectionViewModel _section;
    private readonly Prompt _prompt;
    private readonly PromptViewModelBase _vm;
    private readonly int _index;

    public AddPromptCommand(SectionViewModel section, Prompt prompt, PromptViewModelBase vm, int index)
    {
        _section = section; _prompt = prompt; _vm = vm; _index = index;
    }

    public string Description => "Add prompt";
    public void Execute() => _section.ApplyAddPromptAt(_index, _prompt, _vm);
    public void Undo() => _section.ApplyRemovePrompt(_vm);
    public bool CanMergeWith(IEditCommand next) => false;
    public void MergeWith(IEditCommand next) { }
}

internal sealed class RemovePromptCommand : IEditCommand
{
    private readonly SectionViewModel _section;
    private readonly PromptViewModelBase _vm;
    private readonly Prompt _prompt;
    private readonly int _index;

    public RemovePromptCommand(SectionViewModel section, PromptViewModelBase vm, int index)
    {
        _section = section; _vm = vm; _prompt = vm.Model; _index = index;
    }

    public string Description => "Remove prompt";
    public void Execute() => _section.ApplyRemovePrompt(_vm);
    public void Undo() => _section.ApplyAddPromptAt(_index, _prompt, _vm);
    public bool CanMergeWith(IEditCommand next) => false;
    public void MergeWith(IEditCommand next) { }
}

internal sealed class AddNestedSectionCommand : IEditCommand
{
    private readonly SectionViewModel _parent;
    private readonly SectionViewModel _child;
    private readonly Section _childModel;
    private readonly int _index;

    public AddNestedSectionCommand(SectionViewModel parent, SectionViewModel child, int index)
    {
        _parent = parent; _child = child; _childModel = child.Model; _index = index;
    }

    public string Description => "Add nested section";
    public void Execute() => _parent.ApplyAddNestedSectionAt(_index, _childModel, _child);
    public void Undo() => _parent.ApplyRemoveNestedSection(_child);
    public bool CanMergeWith(IEditCommand next) => false;
    public void MergeWith(IEditCommand next) { }
}

internal sealed class RemoveNestedSectionCommand : IEditCommand
{
    private readonly SectionViewModel _parent;
    private readonly SectionViewModel _child;
    private readonly Section _childModel;
    private readonly int _index;

    public RemoveNestedSectionCommand(SectionViewModel parent, SectionViewModel child, int index)
    {
        _parent = parent; _child = child; _childModel = child.Model; _index = index;
    }

    public string Description => "Remove section";
    public void Execute() => _parent.ApplyRemoveNestedSection(_child);
    public void Undo() => _parent.ApplyAddNestedSectionAt(_index, _childModel, _child);
    public bool CanMergeWith(IEditCommand next) => false;
    public void MergeWith(IEditCommand next) { }
}







internal sealed class MovePromptCommand : IEditCommand
{
    private readonly SectionViewModel _section;
    private readonly int _from;
    private readonly int _to;

    public MovePromptCommand(SectionViewModel section, int from, int to)
    { _section = section; _from = from; _to = to; }

    public string Description => "Reorder prompt";
    public void Execute() => _section.ApplyMovePrompt(_from, _to);
    public void Undo() => _section.ApplyMovePrompt(_to, _from);
    public bool CanMergeWith(IEditCommand next) => false;
    public void MergeWith(IEditCommand next) { }
}

internal sealed class MoveNestedSectionCommand : IEditCommand
{
    private readonly SectionViewModel _parent;
    private readonly int _from;
    private readonly int _to;

    public MoveNestedSectionCommand(SectionViewModel parent, int from, int to)
    { _parent = parent; _from = from; _to = to; }

    public string Description => "Reorder section";
    public void Execute() => _parent.ApplyMoveNestedSection(_from, _to);
    public void Undo() => _parent.ApplyMoveNestedSection(_to, _from);
    public bool CanMergeWith(IEditCommand next) => false;
    public void MergeWith(IEditCommand next) { }
}



internal sealed class MoveTopLevelSectionCommand : IEditCommand
{
    private readonly MainShellViewModel _shell;
    private readonly int _from;
    private readonly int _to;

    public MoveTopLevelSectionCommand(MainShellViewModel shell, int from, int to)
    { _shell = shell; _from = from; _to = to; }

    public string Description => "Reorder top-level section";
    public void Execute() => _shell.ApplyMoveTopLevelSection(_from, _to);
    public void Undo() => _shell.ApplyMoveTopLevelSection(_to, _from);
    public bool CanMergeWith(IEditCommand next) => false;
    public void MergeWith(IEditCommand next) { }
}

internal sealed class AddTopLevelSectionCommand : IEditCommand
{
    private readonly MainShellViewModel _shell;
    private readonly Section _model;
    private readonly SectionViewModel _vm;
    private readonly int _index;

    public AddTopLevelSectionCommand(MainShellViewModel shell, Section model, SectionViewModel vm, int index)
    {
        _shell = shell; _model = model; _vm = vm; _index = index;
    }

    public string Description => "Add top-level section";
    public void Execute() => _shell.ApplyAddTopLevelSectionAt(_index, _model, _vm);
    public void Undo() => _shell.ApplyRemoveTopLevelSection(_vm);
    public bool CanMergeWith(IEditCommand next) => false;
    public void MergeWith(IEditCommand next) { }
}

internal sealed class RemoveTopLevelSectionCommand : IEditCommand
{
    private readonly MainShellViewModel _shell;
    private readonly SectionViewModel _vm;
    private readonly Section _model;
    private readonly int _index;

    public RemoveTopLevelSectionCommand(MainShellViewModel shell, SectionViewModel vm, int index)
    {
        _shell = shell; _vm = vm; _model = vm.Model; _index = index;
    }

    public string Description => "Remove top-level section";
    public void Execute() => _shell.ApplyRemoveTopLevelSection(_vm);
    public void Undo() => _shell.ApplyAddTopLevelSectionAt(_index, _model, _vm);
    public bool CanMergeWith(IEditCommand next) => false;
    public void MergeWith(IEditCommand next) { }
}

/// <summary>
/// The state of a table section: its markers plus its whole subtree.
/// </summary>
public sealed record TableSnapshot(
    string? Kind,
    string? CanAddRows,
    string? MaxRows,
    List<Section> Rows,
    List<Prompt> DirectPrompts);

/// <summary>
/// One undoable table edit, recorded as before/after snapshots.
/// </summary>
/// <remarks>
/// Every table operation — converting a section, adding or removing a column, adding
/// or removing a row, renaming a header — is a mutation of the section tree, because
/// a table has no separate layout object to mutate. So a single command covers all of
/// them, replacing the per-operation apply/restore pairs the old shape required. Less
/// undo bookkeeping is not just shorter; it is one fewer thing that can fall out of
/// step with the model.
/// </remarks>
public sealed class TableEditCommand : IEditCommand
{
    private readonly SectionViewModel _section;
    private readonly TableSnapshot _before;
    private readonly TableSnapshot _after;

    public TableEditCommand(SectionViewModel section, TableSnapshot before, TableSnapshot after)
    {
        _section = section;
        _before = before;
        _after = after;
    }

    public string Description => "Edit table";

    // Restoring a snapshot is idempotent, so applying "after" immediately following
    // the mutation that produced it is a no-op in effect, and redo replays correctly.
    public void Execute() => _section.RestoreTableSnapshot(_after);

    public void Undo() => _section.RestoreTableSnapshot(_before);

    // Every table edit is its own undo step; none of them fold together.
    public bool CanMergeWith(IEditCommand next) => false;

    public void MergeWith(IEditCommand next) =>
        throw new NotSupportedException("Table edits never merge.");
}
