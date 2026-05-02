using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.ViewModels.Editing;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// View-model wrapper for a <see cref="Section"/>. Mirrors the recursive section
/// hierarchy in the rendering tree: each section carries its title, description,
/// nested child sections (recursively), and the typed prompt VMs created via
/// <see cref="PromptViewModelFactory"/>.
///
/// Settable properties (Title, Description, Id) and structural commands
/// (AddPrompt, RemovePrompt, AddNestedSection, RemoveNestedSection, table
/// authoring) drive the template-edit-mode UI; they propagate every change to
/// the underlying <see cref="Section"/> so saving the document persists
/// structural edits. When an <see cref="EditHistory"/> is provided, every
/// mutation is also recorded as an <see cref="IEditCommand"/> so the user can
/// Ctrl+Z out of any edit.
///
/// When the underlying Section has <see cref="Section.TableLayout"/> set, this
/// section renders as a table: the child sections are rows, each row's prompts
/// are cells (one per column, with id <c>{rowId}.{columnId}</c>). For dynamic
/// tables, <see cref="AddRowCommand"/>/<see cref="RemoveRowCommand"/> mutate
/// the underlying Section.Sections list and notify the host via the
/// <c>onPromptAdded</c>/<c>onPromptRemoved</c> callbacks so the shell can
/// (un)subscribe to the new cell prompts' Response events.
/// </summary>
public sealed class SectionViewModel : INotifyPropertyChanged
{
    private readonly Section _section;
    private readonly PromptViewModelFactory _factory;
    private readonly int _depth;
    private readonly Action<PromptViewModelBase>? _onPromptAdded;
    private readonly Action<PromptViewModelBase>? _onPromptRemoved;
    private readonly EditHistory? _history;
    private readonly ObservableCollection<SectionViewModel> _nestedSections;
    private readonly ObservableCollection<PromptViewModelBase> _promptViewModels;
    private IReadOnlyList<TableCellViewModel> _cells = Array.Empty<TableCellViewModel>();

    public SectionViewModel(
        Section section,
        PromptViewModelFactory factory,
        int depth,
        Action<PromptViewModelBase>? onPromptAdded = null,
        Action<PromptViewModelBase>? onPromptRemoved = null,
        EditHistory? history = null)
    {
        _section = section ?? throw new ArgumentNullException(nameof(section));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _depth = depth;
        _onPromptAdded = onPromptAdded;
        _onPromptRemoved = onPromptRemoved;
        _history = history;

        _promptViewModels = new ObservableCollection<PromptViewModelBase>();
        foreach (var prompt in _section.Prompts)
        {
            _promptViewModels.Add(_factory.Create(prompt));
        }

        _nestedSections = new ObservableCollection<SectionViewModel>();
        foreach (var child in _section.Sections)
        {
            _nestedSections.Add(new SectionViewModel(child, _factory, _depth + 1, _onPromptAdded, _onPromptRemoved, _history));
        }

        // If this section is a table, configure each row child with the column
        // layout so its cells can be rendered in column order.
        if (TableLayout != null)
        {
            foreach (var rowVm in _nestedSections)
            {
                rowVm.ConfigureAsTableRow(TableLayout.Columns);
            }
        }

        AddRowCommand = new RelayCommand(AddRow, () => CanAddRow);
        RemoveRowCommand = new RelayCommand<SectionViewModel>(RemoveRow, _ => CanRemoveRow);
        AddPromptCommand = new RelayCommand(() => AddPrompt());
        RemovePromptCommand = new RelayCommand<PromptViewModelBase>(RemovePrompt);
        AddNestedSectionCommand = new RelayCommand(() => AddNestedSection());
        RemoveNestedSectionCommand = new RelayCommand<SectionViewModel>(RemoveNestedSection);
        ConvertToFixedTableCommand = new RelayCommand(ConvertToFixedTable);
        ConvertToDynamicTableCommand = new RelayCommand(ConvertToDynamicTable);
        RemoveTableLayoutCommand = new RelayCommand(RemoveTableLayout);
        AddColumnCommand = new RelayCommand(AddColumn);
        RemoveColumnCommand = new RelayCommand<TableColumn>(RemoveColumn);
        AddFixedRowCommand = new RelayCommand(AddFixedRow);
        RemoveFixedRowCommand = new RelayCommand<SectionViewModel>(RemoveFixedRow);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id
    {
        get => _section.Id;
        set => SetWithUndo(nameof(Id), () => _section.Id, v => _section.Id = v, value ?? string.Empty);
    }

    public string Title
    {
        get => _section.Title;
        set => SetWithUndo(nameof(Title), () => _section.Title, v => _section.Title = v, value ?? string.Empty);
    }

    public string? Description
    {
        get => _section.Description;
        set => SetWithUndo(
            nameof(Description),
            () => _section.Description,
            v => { _section.Description = v; OnPropertyChanged(nameof(HasDescription)); },
            value);
    }

    public int Depth => _depth;
    public bool HasDescription => !string.IsNullOrWhiteSpace(_section.Description);

    /// <summary>Indent depth in pixels — proportional to nesting depth.</summary>
    public double IndentLeft => _depth * 24.0;

    public ObservableCollection<PromptViewModelBase> PromptViewModels => _promptViewModels;
    public ObservableCollection<SectionViewModel> NestedSections => _nestedSections;

    /// <summary>Underlying model — exposed for editor surface and serialization.
    /// Fill-mode rendering should not need this.</summary>
    internal Section Model => _section;

    internal EditHistory? History => _history;

    // ── Structural editing commands (template edit mode) ──

    public IRelayCommand AddPromptCommand { get; }
    public IRelayCommand<PromptViewModelBase> RemovePromptCommand { get; }
    public IRelayCommand AddNestedSectionCommand { get; }
    public IRelayCommand<SectionViewModel> RemoveNestedSectionCommand { get; }

    /// <summary>Append a new default prompt to this section. Routes through the
    /// edit history if one is configured so the operation is undoable.</summary>
    public PromptViewModelBase AddPrompt()
    {
        var prompt = new Prompt
        {
            Id = $"prompt_{Guid.NewGuid():N}",
            Label = "New prompt",
            Hints = new PromptHints { ExpectedDataType = "text" },
        };
        var vm = _factory.Create(prompt);
        var index = _promptViewModels.Count;

        if (_history != null && !_history.IsApplying)
        {
            _history.Execute(new AddPromptCommand(this, prompt, vm, index));
        }
        else
        {
            ApplyAddPromptAt(index, prompt, vm);
        }
        return vm;
    }

    /// <summary>Remove a prompt from this section and notify the shell so it
    /// unsubscribes/disposes the prompt VM. Undoable.</summary>
    public void RemovePrompt(PromptViewModelBase? promptVm)
    {
        if (promptVm is null) return;
        if (!_promptViewModels.Contains(promptVm)) return;

        if (_history != null && !_history.IsApplying)
        {
            var index = _promptViewModels.IndexOf(promptVm);
            _history.Execute(new RemovePromptCommand(this, promptVm, index));
        }
        else
        {
            ApplyRemovePrompt(promptVm);
        }
    }

    /// <summary>Append a new empty nested section to this section. Undoable.</summary>
    public SectionViewModel AddNestedSection()
    {
        var child = new Section
        {
            Id = $"section_{Guid.NewGuid():N}",
            Title = "New section",
        };
        var vm = new SectionViewModel(child, _factory, _depth + 1, _onPromptAdded, _onPromptRemoved, _history);
        var index = _nestedSections.Count;

        if (_history != null && !_history.IsApplying)
        {
            _history.Execute(new AddNestedSectionCommand(this, vm, index));
        }
        else
        {
            ApplyAddNestedSectionAt(index, child, vm);
        }
        return vm;
    }

    /// <summary>Remove a nested section (and all its prompts) from this section. Undoable.</summary>
    public void RemoveNestedSection(SectionViewModel? child)
    {
        if (child is null) return;
        if (!_nestedSections.Contains(child)) return;

        if (_history != null && !_history.IsApplying)
        {
            var index = _nestedSections.IndexOf(child);
            _history.Execute(new RemoveNestedSectionCommand(this, child, index));
        }
        else
        {
            ApplyRemoveNestedSection(child);
        }
    }

    /// <summary>Move a prompt within this section from one index to another. Undoable.</summary>
    public void MovePrompt(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;
        if (fromIndex < 0 || fromIndex >= _promptViewModels.Count) return;
        if (toIndex < 0 || toIndex >= _promptViewModels.Count) return;

        if (_history != null && !_history.IsApplying)
            _history.Execute(new MovePromptCommand(this, fromIndex, toIndex));
        else
            ApplyMovePrompt(fromIndex, toIndex);
    }

    /// <summary>Move a nested section within this section from one index to another. Undoable.</summary>
    public void MoveNestedSection(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;
        if (fromIndex < 0 || fromIndex >= _nestedSections.Count) return;
        if (toIndex < 0 || toIndex >= _nestedSections.Count) return;

        if (_history != null && !_history.IsApplying)
            _history.Execute(new MoveNestedSectionCommand(this, fromIndex, toIndex));
        else
            ApplyMoveNestedSection(fromIndex, toIndex);
    }

    /// <summary>Reorder a column in the table layout. Cell prompts in every row
    /// are not moved — they're keyed by id, so column visual order is the only
    /// thing that changes. Undoable.</summary>
    public void MoveColumn(int fromIndex, int toIndex)
    {
        if (!IsTableSection) return;
        var def = _section.TableLayout!;
        if (fromIndex == toIndex) return;
        if (fromIndex < 0 || fromIndex >= def.Columns.Count) return;
        if (toIndex < 0 || toIndex >= def.Columns.Count) return;

        if (_history != null && !_history.IsApplying)
            _history.Execute(new MoveColumnCommand(this, fromIndex, toIndex));
        else
            ApplyMoveColumn(fromIndex, toIndex);
    }

    /// <summary>Reorder a fixed row. Undoable.</summary>
    public void MoveFixedRow(int fromIndex, int toIndex)
    {
        if (!IsTableSection) return;
        var def = _section.TableLayout!;
        if (def.FixedRows == null) return;
        if (fromIndex == toIndex) return;
        if (fromIndex < 0 || fromIndex >= def.FixedRows.Count) return;
        if (toIndex < 0 || toIndex >= def.FixedRows.Count) return;

        if (_history != null && !_history.IsApplying)
            _history.Execute(new MoveFixedRowCommand(this, fromIndex, toIndex));
        else
            ApplyMoveFixedRow(fromIndex, toIndex);
    }

    // ── Apply* methods: raw mutations used by both public methods and command Execute/Undo. ──

    internal void ApplyMovePrompt(int fromIndex, int toIndex)
    {
        var prompt = _section.Prompts[fromIndex];
        _section.Prompts.RemoveAt(fromIndex);
        _section.Prompts.Insert(toIndex, prompt);
        var vm = _promptViewModels[fromIndex];
        _promptViewModels.RemoveAt(fromIndex);
        _promptViewModels.Insert(toIndex, vm);
    }

    internal void ApplyMoveNestedSection(int fromIndex, int toIndex)
    {
        var sec = _section.Sections[fromIndex];
        _section.Sections.RemoveAt(fromIndex);
        _section.Sections.Insert(toIndex, sec);
        var vm = _nestedSections[fromIndex];
        _nestedSections.RemoveAt(fromIndex);
        _nestedSections.Insert(toIndex, vm);
    }

    internal void ApplyMoveColumn(int fromIndex, int toIndex)
    {
        var def = _section.TableLayout!;
        var col = def.Columns[fromIndex];
        def.Columns.RemoveAt(fromIndex);
        def.Columns.Insert(toIndex, col);
        ReconfigureAllRowsAsTableRows();
        OnPropertyChanged(nameof(Columns));
    }

    internal void ApplyMoveFixedRow(int fromIndex, int toIndex)
    {
        var def = _section.TableLayout!;
        var rows = def.FixedRows!;
        var row = rows[fromIndex];
        rows.RemoveAt(fromIndex);
        rows.Insert(toIndex, row);
        // Also move the corresponding row sub-section so the rendered order matches.
        var rowVm = _nestedSections[fromIndex];
        _nestedSections.RemoveAt(fromIndex);
        _nestedSections.Insert(toIndex, rowVm);
        var rowSec = _section.Sections[fromIndex];
        _section.Sections.RemoveAt(fromIndex);
        _section.Sections.Insert(toIndex, rowSec);
    }



    internal void ApplyAddPromptAt(int index, Prompt prompt, PromptViewModelBase vm)
    {
        if (index < 0 || index > _section.Prompts.Count) index = _section.Prompts.Count;
        _section.Prompts.Insert(index, prompt);
        if (index > _promptViewModels.Count) index = _promptViewModels.Count;
        _promptViewModels.Insert(index, vm);
        _onPromptAdded?.Invoke(vm);
    }

    internal void ApplyRemovePrompt(PromptViewModelBase vm)
    {
        if (!_promptViewModels.Contains(vm)) return;
        _onPromptRemoved?.Invoke(vm);
        _section.Prompts.Remove(vm.Model);
        _promptViewModels.Remove(vm);
    }

    internal void ApplyAddNestedSectionAt(int index, Section model, SectionViewModel vm)
    {
        if (index < 0 || index > _section.Sections.Count) index = _section.Sections.Count;
        _section.Sections.Insert(index, model);
        if (index > _nestedSections.Count) index = _nestedSections.Count;
        _nestedSections.Insert(index, vm);
    }

    internal void ApplyRemoveNestedSection(SectionViewModel child)
    {
        if (!_nestedSections.Contains(child)) return;
        // Pull every prompt under the removed subtree out of the shell-tracked list.
        WalkPromptsRecursively(child, vm => _onPromptRemoved?.Invoke(vm));
        _section.Sections.Remove(child._section);
        _nestedSections.Remove(child);
    }

    private static void WalkPromptsRecursively(SectionViewModel s, Action<PromptViewModelBase> visit)
    {
        foreach (var p in s._promptViewModels) visit(p);
        foreach (var child in s._nestedSections) WalkPromptsRecursively(child, visit);
    }

    // ── Table mode ──

    /// <summary>Layout for tabular rendering, or null when this is a regular section.</summary>
    public TableDefinition? TableLayout => _section.TableLayout;

    /// <summary>True when this section should render as a table grid.</summary>
    public bool IsTableSection => _section.TableLayout != null;
    public bool IsRegularSection => _section.TableLayout == null;

    public bool IsFixedTable => TableLayout?.IsFixedTable ?? false;
    public bool IsDynamicTable => TableLayout?.IsDynamicTable ?? false;

    /// <summary>Column headers from the table layout — empty for non-table sections.</summary>
    public IReadOnlyList<TableColumn> Columns => (IReadOnlyList<TableColumn>?)TableLayout?.Columns ?? Array.Empty<TableColumn>();

    /// <summary>Cells in column order — populated only when this section is a row of a parent table-section.</summary>
    public IReadOnlyList<TableCellViewModel> Cells
    {
        get => _cells;
        private set { _cells = value; OnPropertyChanged(); }
    }

    public bool CanAddRow =>
        IsDynamicTable
        && TableLayout!.DynamicRows is { } d
        && _nestedSections.Count < d.MaxRows;

    public bool CanRemoveRow =>
        IsDynamicTable
        && TableLayout!.DynamicRows is { } d
        && _nestedSections.Count > d.MinRows
        && _nestedSections.Count > 0;

    public IRelayCommand AddRowCommand { get; }
    public IRelayCommand<SectionViewModel> RemoveRowCommand { get; }

    // ── Table authoring (edit mode) ──

    public IRelayCommand ConvertToFixedTableCommand { get; }
    public IRelayCommand ConvertToDynamicTableCommand { get; }
    public IRelayCommand RemoveTableLayoutCommand { get; }
    public IRelayCommand AddColumnCommand { get; }
    public IRelayCommand<TableColumn> RemoveColumnCommand { get; }
    public IRelayCommand AddFixedRowCommand { get; }
    public IRelayCommand<SectionViewModel> RemoveFixedRowCommand { get; }

    public void ConvertToFixedTable()
    {
        if (IsTableSection) return;
        if (_history != null && !_history.IsApplying)
            _history.Execute(new ConvertToTableCommand(this, fixedRows: true));
        else
            ApplyConvertToFixedTable();
    }

    public void ConvertToDynamicTable()
    {
        if (IsTableSection) return;
        if (_history != null && !_history.IsApplying)
            _history.Execute(new ConvertToTableCommand(this, fixedRows: false));
        else
            ApplyConvertToDynamicTable();
    }

    public void RemoveTableLayout()
    {
        if (!IsTableSection) return;
        if (_history != null && !_history.IsApplying)
            _history.Execute(new RemoveTableLayoutCommand(this));
        else
            ApplyRemoveTableLayout();
    }

    public void AddColumn()
    {
        if (!IsTableSection) return;
        if (_history != null && !_history.IsApplying)
            _history.Execute(new AddColumnCommand(this));
        else
            ApplyAddColumn();
    }

    public void RemoveColumn(TableColumn? column)
    {
        if (column is null) return;
        if (!IsTableSection) return;
        var def = _section.TableLayout!;
        if (!def.Columns.Contains(column)) return;
        if (def.Columns.Count <= 1) return;

        if (_history != null && !_history.IsApplying)
        {
            var index = def.Columns.IndexOf(column);
            _history.Execute(new RemoveColumnCommand(this, column, index));
        }
        else
        {
            ApplyRemoveColumnAndCapture(column);
        }
    }

    public void AddFixedRow()
    {
        if (!IsTableSection) return;
        if (_section.TableLayout!.FixedRows == null) return;
        if (_history != null && !_history.IsApplying)
            _history.Execute(new AddFixedRowCommand(this));
        else
            ApplyAddFixedRow();
    }

    public void RemoveFixedRow(SectionViewModel? row)
    {
        if (row is null) return;
        if (!IsTableSection) return;
        var def = _section.TableLayout!;
        if (def.FixedRows == null) return;
        if (!_nestedSections.Contains(row)) return;
        if (_nestedSections.Count <= 1) return;

        if (_history != null && !_history.IsApplying)
        {
            var fixedRow = def.FixedRows.FirstOrDefault(r => r.Id == row.Id);
            if (fixedRow == null) return;
            var vmIndex = _nestedSections.IndexOf(row);
            var layoutIndex = def.FixedRows.IndexOf(fixedRow);
            _history.Execute(new RemoveFixedRowCommand(this, row, fixedRow, vmIndex, layoutIndex));
        }
        else
        {
            ApplyRemoveFixedRow(row);
        }
    }

    // ── Apply helpers for table commands ──

    internal TableLayoutSnapshot SnapshotTableState() => new()
    {
        Layout = _section.TableLayout,
        Rows = new List<Section>(_section.Sections),
        DirectPrompts = new List<Prompt>(_section.Prompts),
    };

    internal void ApplyConvertToFixedTable()
    {
        // Drop any preexisting prompts directly on the section — table sections
        // hold structure, not free-floating prompts.
        foreach (var p in _promptViewModels.ToList()) _onPromptRemoved?.Invoke(p);
        _section.Prompts.Clear();
        _promptViewModels.Clear();

        _section.TableLayout = new TableDefinition
        {
            Columns = new List<TableColumn> { new() { Id = "col1", Label = "Column 1", Type = "text" } },
            FixedRows = new List<FixedRow> { new() { Id = "row1", Label = "Row 1" } },
        };
        RebuildRowSubSectionsFromTableLayout();
        NotifyTableShapeChanged();
    }

    internal void ApplyConvertToDynamicTable()
    {
        foreach (var p in _promptViewModels.ToList()) _onPromptRemoved?.Invoke(p);
        _section.Prompts.Clear();
        _promptViewModels.Clear();

        _section.TableLayout = new TableDefinition
        {
            Columns = new List<TableColumn> { new() { Id = "col1", Label = "Column 1", Type = "text" } },
            DynamicRows = new DynamicRowConfig { MinRows = 0, MaxRows = 100, RowLabel = "Row" },
        };
        NotifyTableShapeChanged();
    }

    internal void ApplyRemoveTableLayout()
    {
        foreach (var rowVm in _nestedSections.ToList())
        {
            foreach (var p in rowVm._promptViewModels) _onPromptRemoved?.Invoke(p);
        }
        _section.Sections.Clear();
        _nestedSections.Clear();
        _section.TableLayout = null;
        NotifyTableShapeChanged();
    }

    internal void ApplyRestoreTableSnapshot(TableLayoutSnapshot snap)
    {
        // Tear down current state.
        foreach (var rowVm in _nestedSections.ToList())
        {
            foreach (var p in rowVm._promptViewModels) _onPromptRemoved?.Invoke(p);
        }
        foreach (var p in _promptViewModels.ToList()) _onPromptRemoved?.Invoke(p);
        _section.Sections.Clear();
        _section.Prompts.Clear();
        _nestedSections.Clear();
        _promptViewModels.Clear();

        // Restore prompts on the section directly.
        _section.TableLayout = snap.Layout;
        foreach (var prompt in snap.DirectPrompts)
        {
            _section.Prompts.Add(prompt);
            var vm = _factory.Create(prompt);
            _promptViewModels.Add(vm);
            _onPromptAdded?.Invoke(vm);
        }
        // Restore rows.
        foreach (var rowModel in snap.Rows)
        {
            _section.Sections.Add(rowModel);
            var rowVm = new SectionViewModel(rowModel, _factory, _depth + 1, _onPromptAdded, _onPromptRemoved, _history);
            if (_section.TableLayout != null) rowVm.ConfigureAsTableRow(_section.TableLayout.Columns);
            _nestedSections.Add(rowVm);
            foreach (var p in rowVm._promptViewModels) _onPromptAdded?.Invoke(p);
        }
        NotifyTableShapeChanged();
    }

    internal (TableColumn, List<PromptViewModelBase>) ApplyAddColumn()
    {
        var def = _section.TableLayout!;
        var ordinal = def.Columns.Count + 1;
        var col = new TableColumn { Id = $"col{ordinal}", Label = $"Column {ordinal}", Type = "text" };
        def.Columns.Add(col);
        var added = new List<PromptViewModelBase>();
        foreach (var rowVm in _nestedSections)
        {
            var cellPrompt = new Prompt
            {
                Id = $"{rowVm._section.Id}.{col.Id}",
                Label = col.Label,
                Hints = new PromptHints { ExpectedDataType = col.Type },
            };
            rowVm._section.Prompts.Add(cellPrompt);
            var promptVm = _factory.Create(cellPrompt);
            rowVm._promptViewModels.Add(promptVm);
            _onPromptAdded?.Invoke(promptVm);
            added.Add(promptVm);
        }
        ReconfigureAllRowsAsTableRows();
        NotifyTableShapeChanged();
        return (col, added);
    }

    internal void ApplyAddColumnRestore(TableColumn col, List<PromptViewModelBase> cellVms)
    {
        var def = _section.TableLayout!;
        def.Columns.Add(col);
        for (var i = 0; i < _nestedSections.Count && i < cellVms.Count; i++)
        {
            var rowVm = _nestedSections[i];
            var promptVm = cellVms[i];
            rowVm._section.Prompts.Add(promptVm.Model);
            rowVm._promptViewModels.Add(promptVm);
            _onPromptAdded?.Invoke(promptVm);
        }
        ReconfigureAllRowsAsTableRows();
        NotifyTableShapeChanged();
    }

    internal void ApplyAddColumnAtRestore(int index, TableColumn col, List<PromptViewModelBase> cellVms)
    {
        var def = _section.TableLayout!;
        if (index < 0 || index > def.Columns.Count) index = def.Columns.Count;
        def.Columns.Insert(index, col);
        for (var i = 0; i < _nestedSections.Count && i < cellVms.Count; i++)
        {
            var rowVm = _nestedSections[i];
            var promptVm = cellVms[i];
            rowVm._section.Prompts.Add(promptVm.Model);
            rowVm._promptViewModels.Add(promptVm);
            _onPromptAdded?.Invoke(promptVm);
        }
        ReconfigureAllRowsAsTableRows();
        NotifyTableShapeChanged();
    }

    internal List<PromptViewModelBase> ApplyRemoveColumnAndCapture(TableColumn column)
    {
        var def = _section.TableLayout!;
        def.Columns.Remove(column);
        var dropped = new List<PromptViewModelBase>();
        foreach (var rowVm in _nestedSections)
        {
            var matching = rowVm._promptViewModels.FirstOrDefault(vm => vm.Id == $"{rowVm._section.Id}.{column.Id}");
            if (matching != null)
            {
                _onPromptRemoved?.Invoke(matching);
                rowVm._section.Prompts.Remove(matching.Model);
                rowVm._promptViewModels.Remove(matching);
                dropped.Add(matching);
            }
        }
        ReconfigureAllRowsAsTableRows();
        NotifyTableShapeChanged();
        return dropped;
    }

    internal void ApplyRemoveColumn(TableColumn column, List<PromptViewModelBase> droppedCellVms)
    {
        // Used by AddColumn.Undo: drop the column we previously added.
        var def = _section.TableLayout!;
        def.Columns.Remove(column);
        foreach (var vm in droppedCellVms)
        {
            var rowVm = _nestedSections.FirstOrDefault(r => r._promptViewModels.Contains(vm));
            if (rowVm == null) continue;
            _onPromptRemoved?.Invoke(vm);
            rowVm._section.Prompts.Remove(vm.Model);
            rowVm._promptViewModels.Remove(vm);
        }
        ReconfigureAllRowsAsTableRows();
        NotifyTableShapeChanged();
    }

    internal (FixedRow, SectionViewModel, int) ApplyAddFixedRow()
    {
        var def = _section.TableLayout!;
        var ordinal = def.FixedRows!.Count + 1;
        var rowId = $"row{ordinal}";
        while (def.FixedRows.Any(r => r.Id == rowId))
        {
            ordinal++;
            rowId = $"row{ordinal}";
        }
        var fixedRow = new FixedRow { Id = rowId, Label = $"Row {ordinal}" };
        def.FixedRows.Add(fixedRow);

        var rowSection = new Section { Id = rowId, Title = $"Row {ordinal}" };
        foreach (var col in def.Columns)
        {
            rowSection.Prompts.Add(new Prompt
            {
                Id = $"{rowId}.{col.Id}",
                Label = col.Label,
                Hints = new PromptHints { ExpectedDataType = col.Type },
            });
        }
        _section.Sections.Add(rowSection);
        var rowVm = new SectionViewModel(rowSection, _factory, _depth + 1, _onPromptAdded, _onPromptRemoved, _history);
        rowVm.ConfigureAsTableRow(def.Columns);
        _nestedSections.Add(rowVm);
        foreach (var p in rowVm._promptViewModels) _onPromptAdded?.Invoke(p);
        NotifyTableShapeChanged();
        return (fixedRow, rowVm, _nestedSections.Count - 1);
    }

    internal void ApplyAddFixedRowRestore(FixedRow row, SectionViewModel rowVm, int vmIndex)
    {
        var def = _section.TableLayout!;
        def.FixedRows!.Add(row);
        _section.Sections.Add(rowVm._section);
        if (vmIndex < 0 || vmIndex > _nestedSections.Count) vmIndex = _nestedSections.Count;
        _nestedSections.Insert(vmIndex, rowVm);
        foreach (var p in rowVm._promptViewModels) _onPromptAdded?.Invoke(p);
        rowVm.ConfigureAsTableRow(def.Columns);
        NotifyTableShapeChanged();
    }

    internal void ApplyAddFixedRowAtRestore(int layoutIndex, FixedRow row, SectionViewModel rowVm, int vmIndex)
    {
        var def = _section.TableLayout!;
        var fixedRows = def.FixedRows!;
        if (layoutIndex < 0 || layoutIndex > fixedRows.Count) layoutIndex = fixedRows.Count;
        fixedRows.Insert(layoutIndex, row);
        _section.Sections.Add(rowVm._section);
        if (vmIndex < 0 || vmIndex > _nestedSections.Count) vmIndex = _nestedSections.Count;
        _nestedSections.Insert(vmIndex, rowVm);
        foreach (var p in rowVm._promptViewModels) _onPromptAdded?.Invoke(p);
        rowVm.ConfigureAsTableRow(def.Columns);
        NotifyTableShapeChanged();
    }

    internal void ApplyRemoveFixedRow(SectionViewModel row)
    {
        var def = _section.TableLayout!;
        if (def.FixedRows == null) return;
        if (!_nestedSections.Contains(row)) return;
        foreach (var p in row._promptViewModels) _onPromptRemoved?.Invoke(p);
        def.FixedRows.RemoveAll(r => r.Id == row.Id);
        _section.Sections.Remove(row.Model);
        _nestedSections.Remove(row);
        NotifyTableShapeChanged();
    }

    /// <summary>Builds the cell view-models for this section's prompts in the order
    /// declared by <paramref name="columns"/>. Called by the parent table-section
    /// during construction, and again on every dynamic add/remove so labels stay
    /// renumbered.</summary>
    internal void ConfigureAsTableRow(IReadOnlyList<TableColumn> columns)
    {
        var built = new List<TableCellViewModel>(columns.Count);
        foreach (var col in columns)
        {
            var cellId = $"{_section.Id}.{col.Id}";
            var promptVm = _promptViewModels.FirstOrDefault(vm => vm.Id == cellId);
            if (promptVm == null) continue;
            built.Add(new TableCellViewModel(promptVm, col));
        }
        Cells = built;
    }

    /// <summary>Append a new dynamic row at fill time. Builds a fresh row sub-section
    /// with one cell-prompt per column, wires it into the model and VM trees, and
    /// notifies the host so the shell can subscribe to the new prompts' Response
    /// events. Not undoable — fill-time dynamic add/remove is not part of the
    /// authoring history.</summary>
    public void AddRow()
    {
        if (TableLayout?.DynamicRows is null) return;
        if (_nestedSections.Count >= TableLayout.DynamicRows.MaxRows) return;

        var def = TableLayout;
        var rowId = Guid.NewGuid().ToString("N");
        var ordinal = _nestedSections.Count + 1;
        var rowSection = new Section
        {
            Id = rowId,
            Title = $"{def.DynamicRows!.RowLabel} {ordinal}",
            Prompts = def.Columns.Select(col => new Prompt
            {
                Id = $"{rowId}.{col.Id}",
                Label = col.Label,
                Hints = new PromptHints
                {
                    ExpectedDataType = col.Type,
                    Placeholder = col.Placeholder,
                    HelpText = col.HelpText,
                    SuggestedValues = col.SuggestedValues?.ToList() ?? new List<string>(),
                },
            }).ToList(),
        };
        _section.Sections.Add(rowSection);

        var rowVm = new SectionViewModel(rowSection, _factory, _depth + 1, _onPromptAdded, _onPromptRemoved, _history);
        rowVm.ConfigureAsTableRow(def.Columns);
        _nestedSections.Add(rowVm);

        foreach (var promptVm in rowVm.PromptViewModels)
        {
            _onPromptAdded?.Invoke(promptVm);
        }

        NotifyTableMembershipChanged();
    }

    /// <summary>Remove a dynamic row at fill time. Renumbers remaining row labels.</summary>
    public void RemoveRow(SectionViewModel? row)
    {
        if (row is null) return;
        if (TableLayout?.DynamicRows is null) return;
        if (_nestedSections.Count <= TableLayout.DynamicRows.MinRows) return;
        if (!_nestedSections.Contains(row)) return;

        foreach (var promptVm in row._promptViewModels)
        {
            _onPromptRemoved?.Invoke(promptVm);
        }

        _section.Sections.Remove(row._section);
        _nestedSections.Remove(row);

        var rowLabel = TableLayout.DynamicRows.RowLabel;
        for (var i = 0; i < _nestedSections.Count; i++)
        {
            var sib = _nestedSections[i];
            sib._section.Title = $"{rowLabel} {i + 1}";
            sib.OnPropertyChanged(nameof(Title));
        }

        NotifyTableMembershipChanged();
    }

    private void RebuildRowSubSectionsFromTableLayout()
    {
        foreach (var rowVm in _nestedSections.ToList())
        {
            foreach (var p in rowVm._promptViewModels) _onPromptRemoved?.Invoke(p);
        }
        _section.Sections.Clear();
        _nestedSections.Clear();

        var def = _section.TableLayout!;
        if (def.FixedRows != null)
        {
            foreach (var fixedRow in def.FixedRows)
            {
                var rowSection = new Section { Id = fixedRow.Id, Title = fixedRow.Label };
                foreach (var col in def.Columns)
                {
                    rowSection.Prompts.Add(new Prompt
                    {
                        Id = $"{fixedRow.Id}.{col.Id}",
                        Label = col.Label,
                        Hints = new PromptHints { ExpectedDataType = col.Type },
                    });
                }
                _section.Sections.Add(rowSection);
                var rowVm = new SectionViewModel(rowSection, _factory, _depth + 1, _onPromptAdded, _onPromptRemoved, _history);
                rowVm.ConfigureAsTableRow(def.Columns);
                _nestedSections.Add(rowVm);
                foreach (var p in rowVm._promptViewModels) _onPromptAdded?.Invoke(p);
            }
        }
    }

    private void ReconfigureAllRowsAsTableRows()
    {
        if (!IsTableSection) return;
        foreach (var rowVm in _nestedSections)
        {
            rowVm.ConfigureAsTableRow(TableLayout!.Columns);
        }
    }

    private void NotifyTableShapeChanged()
    {
        OnPropertyChanged(nameof(IsTableSection));
        OnPropertyChanged(nameof(IsRegularSection));
        OnPropertyChanged(nameof(IsFixedTable));
        OnPropertyChanged(nameof(IsDynamicTable));
        OnPropertyChanged(nameof(TableLayout));
        OnPropertyChanged(nameof(Columns));
        NotifyTableMembershipChanged();
    }

    private void NotifyTableMembershipChanged()
    {
        OnPropertyChanged(nameof(CanAddRow));
        OnPropertyChanged(nameof(CanRemoveRow));
        AddRowCommand.NotifyCanExecuteChanged();
        RemoveRowCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Helper for property setters. Routes the mutation through the
    /// edit history (when one is configured and not currently applying) so the
    /// edit is undoable + mergeable, otherwise applies it directly.</summary>
    private void SetWithUndo<T>(string propertyName, Func<T> getter, Action<T> applySetter, T newValue)
    {
        var oldValue = getter();
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue)) return;

        if (_history?.IsApplying == true)
        {
            applySetter(newValue);
            OnPropertyChanged(propertyName);
            return;
        }
        if (_history != null)
        {
            _history.Execute(new PropertyEditCommand<T>(
                this, propertyName,
                v => { applySetter(v); OnPropertyChanged(propertyName); },
                oldValue, newValue));
        }
        else
        {
            applySetter(newValue);
            OnPropertyChanged(propertyName);
        }
    }

    internal void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
