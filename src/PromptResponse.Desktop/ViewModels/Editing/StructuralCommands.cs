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

/// <summary>Snapshot of a section's table layout so a destructive convert /
/// strip operation can be fully reversed.</summary>
internal sealed class TableLayoutSnapshot
{
    public TableDefinition? Layout { get; init; }
    public List<Section> Rows { get; init; } = new();
    public List<Prompt> DirectPrompts { get; init; } = new();
}

internal sealed class ConvertToTableCommand : IEditCommand
{
    private readonly SectionViewModel _section;
    private readonly bool _fixedRows;
    private TableLayoutSnapshot? _before;

    public ConvertToTableCommand(SectionViewModel section, bool fixedRows)
    {
        _section = section; _fixedRows = fixedRows;
    }

    public string Description => _fixedRows ? "Convert to fixed table" : "Convert to dynamic table";
    public void Execute()
    {
        _before ??= _section.SnapshotTableState();
        if (_fixedRows) _section.ApplyConvertToFixedTable(); else _section.ApplyConvertToDynamicTable();
    }
    public void Undo() => _section.ApplyRestoreTableSnapshot(_before!);
    public bool CanMergeWith(IEditCommand next) => false;
    public void MergeWith(IEditCommand next) { }
}

internal sealed class RemoveTableLayoutCommand : IEditCommand
{
    private readonly SectionViewModel _section;
    private TableLayoutSnapshot? _before;

    public RemoveTableLayoutCommand(SectionViewModel section)
    {
        _section = section;
    }

    public string Description => "Remove table layout";
    public void Execute()
    {
        _before ??= _section.SnapshotTableState();
        _section.ApplyRemoveTableLayout();
    }
    public void Undo() => _section.ApplyRestoreTableSnapshot(_before!);
    public bool CanMergeWith(IEditCommand next) => false;
    public void MergeWith(IEditCommand next) { }
}

internal sealed class AddColumnCommand : IEditCommand
{
    private readonly SectionViewModel _section;
    private TableColumn? _column;
    private List<PromptViewModelBase> _addedCellVms = new();

    public AddColumnCommand(SectionViewModel section) { _section = section; }

    public string Description => "Add column";
    public void Execute()
    {
        if (_column == null)
        {
            // First execute: synthesize the new column + cell prompts.
            (_column, _addedCellVms) = _section.ApplyAddColumn();
        }
        else
        {
            // Redo: reuse the captured column + cell vm list.
            _section.ApplyAddColumnRestore(_column, _addedCellVms);
        }
    }
    public void Undo()
    {
        if (_column != null) _section.ApplyRemoveColumn(_column, _addedCellVms);
    }
    public bool CanMergeWith(IEditCommand next) => false;
    public void MergeWith(IEditCommand next) { }
}

internal sealed class RemoveColumnCommand : IEditCommand
{
    private readonly SectionViewModel _section;
    private readonly TableColumn _column;
    private readonly int _index;
    private List<PromptViewModelBase> _droppedCellVms = new();

    public RemoveColumnCommand(SectionViewModel section, TableColumn column, int index)
    {
        _section = section; _column = column; _index = index;
    }

    public string Description => "Remove column";
    public void Execute()
    {
        _droppedCellVms = _section.ApplyRemoveColumnAndCapture(_column);
    }
    public void Undo()
    {
        _section.ApplyAddColumnAtRestore(_index, _column, _droppedCellVms);
    }
    public bool CanMergeWith(IEditCommand next) => false;
    public void MergeWith(IEditCommand next) { }
}

internal sealed class AddFixedRowCommand : IEditCommand
{
    private readonly SectionViewModel _section;
    private FixedRow? _row;
    private SectionViewModel? _rowVm;
    private int _vmIndex;

    public AddFixedRowCommand(SectionViewModel section) { _section = section; }

    public string Description => "Add fixed row";
    public void Execute()
    {
        if (_row == null)
        {
            (_row, _rowVm, _vmIndex) = _section.ApplyAddFixedRow();
        }
        else
        {
            _section.ApplyAddFixedRowRestore(_row, _rowVm!, _vmIndex);
        }
    }
    public void Undo()
    {
        if (_row != null && _rowVm != null) _section.ApplyRemoveFixedRow(_rowVm);
    }
    public bool CanMergeWith(IEditCommand next) => false;
    public void MergeWith(IEditCommand next) { }
}

internal sealed class RemoveFixedRowCommand : IEditCommand
{
    private readonly SectionViewModel _section;
    private readonly SectionViewModel _rowVm;
    private readonly FixedRow _row;
    private readonly int _vmIndex;
    private readonly int _layoutIndex;

    public RemoveFixedRowCommand(SectionViewModel section, SectionViewModel rowVm, FixedRow row, int vmIndex, int layoutIndex)
    {
        _section = section; _rowVm = rowVm; _row = row; _vmIndex = vmIndex; _layoutIndex = layoutIndex;
    }

    public string Description => "Remove fixed row";
    public void Execute() => _section.ApplyRemoveFixedRow(_rowVm);
    public void Undo() => _section.ApplyAddFixedRowAtRestore(_layoutIndex, _row, _rowVm, _vmIndex);
    public bool CanMergeWith(IEditCommand next) => false;
    public void MergeWith(IEditCommand next) { }
}
