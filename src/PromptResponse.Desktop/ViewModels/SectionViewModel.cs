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
    private readonly TablePresentationSynchronizer _tablePresentation;
    private readonly TableMutationCoordinator _tableMutation;
    private readonly SectionStructureCoordinator _structure;
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

        _tablePresentation = new TablePresentationSynchronizer(this, _nestedSections);
        _tableMutation = new TableMutationCoordinator(this);
        _structure = new SectionStructureCoordinator(this);

        // If this section is a table, configure each row child with the column
        // layout so its cells can be rendered in column order.
        if (_section.IsTable)
        {
            _tablePresentation.Refresh();
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
        RemoveColumnCommand = new RelayCommand<TableColumnViewModel>(RemoveColumn);
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
        => _structure.AddPrompt();

    /// <summary>Remove a prompt from this section and notify the shell so it
    /// unsubscribes/disposes the prompt VM. Undoable.</summary>
    public void RemovePrompt(PromptViewModelBase? promptVm)
        => _structure.RemovePrompt(promptVm);

    /// <summary>Append a new nested section to this section. Undoable.</summary>
    public SectionViewModel AddNestedSection()
        => _structure.AddNestedSection();

    /// <summary>Remove a nested section (and all its prompts) from this section. Undoable.</summary>
    public void RemoveNestedSection(SectionViewModel? child)
        => _structure.RemoveNestedSection(child);

    /// <summary>Move a prompt within this section from one index to another. Undoable.</summary>
    public void MovePrompt(int fromIndex, int toIndex)
        => _structure.MovePrompt(fromIndex, toIndex);

    /// <summary>Move a nested section within this section from one index to another. Undoable.</summary>
    public void MoveNestedSection(int fromIndex, int toIndex)
        => _structure.MoveNestedSection(fromIndex, toIndex);

    /// <summary>
    /// Reorder a column. A column is a position in every instance's prompt list, so
    /// reordering one moves that prompt in every row — the cells travel with their
    /// header, because they are the same thing. Undoable.
    /// </summary>
    public void MoveColumn(int fromIndex, int toIndex)
    {
        if (!IsTableSection || fromIndex == toIndex) return;
        var count = Columns.Count;
        if (fromIndex < 0 || fromIndex >= count || toIndex < 0 || toIndex >= count) return;
        EditTable(() => ApplyMoveColumn(fromIndex, toIndex));
    }

    /// <summary>Reorder an instance. Undoable.</summary>
    public void MoveFixedRow(int fromIndex, int toIndex)
    {
        if (!IsTableSection || fromIndex == toIndex) return;
        var count = _nestedSections.Count;
        if (fromIndex < 0 || fromIndex >= count || toIndex < 0 || toIndex >= count) return;
        EditTable(() => ApplyMoveFixedRow(fromIndex, toIndex));
    }

    // ── Apply* methods: raw mutations used by both public methods and command Execute/Undo. ──

    internal void ApplyMovePrompt(int fromIndex, int toIndex)
        => _structure.ApplyMovePrompt(fromIndex, toIndex);

    internal void ApplyMoveNestedSection(int fromIndex, int toIndex)
        => _structure.ApplyMoveNestedSection(fromIndex, toIndex);

    internal void ApplyMoveColumn(int fromIndex, int toIndex)
        => _tableMutation.MoveColumn(fromIndex, toIndex);

    internal void ApplyMoveFixedRow(int fromIndex, int toIndex)
        => _tableMutation.MoveRow(fromIndex, toIndex);



    internal void ApplyAddPromptAt(int index, Prompt prompt, PromptViewModelBase vm)
        => _structure.ApplyAddPromptAt(index, prompt, vm);

    internal void ApplyRemovePrompt(PromptViewModelBase vm)
        => _structure.ApplyRemovePrompt(vm);

    internal void ApplyAddNestedSectionAt(int index, Section model, SectionViewModel vm)
        => _structure.ApplyAddNestedSectionAt(index, model, vm);

    internal void ApplyRemoveNestedSection(SectionViewModel child)
        => _structure.ApplyRemoveNestedSection(child);

    // ── Table mode ──
    //
    // A table introduces no new primitive: rows are ordinary child sections and cells
    // are ordinary prompts. A "column" is simply the prompt at a given position,
    // repeated across every instance — so every table edit here is a mutation of the
    // section tree. TableMutationCoordinator owns the synchronized model mutation
    // and one snapshot-based undo covers all of them.

    /// <summary>True when this section's child sections are repeating instances.</summary>
    public bool IsTableSection => _section.IsTable;
    public bool IsRegularSection => !_section.IsTable;

    /// <summary>A table whose instances cannot be added to or removed at fill time.</summary>
    public bool IsFixedTable => IsTableSection && !_section.AllowsAddingRows;

    /// <summary>A table whose instances a filler may add to or remove.</summary>
    public bool IsDynamicTable => _section.AllowsAddingRows;

    /// <summary>Advisory maximum instance count, as text. Blank means no advisory cap.</summary>
    public string MaxRowsText
    {
        get => _section.MaxRows ?? string.Empty;
        set
        {
            var v = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (_section.MaxRows == v) return;
            _section.MaxRows = v;
            OnPropertyChanged();
            NotifyTableMembershipChanged();
        }
    }

    /// <summary>
    /// Column headers, derived from the first instance's prompts. Backed by an
    /// ObservableCollection so the editor refreshes as the shape changes.
    /// </summary>
    public ObservableCollection<TableColumnViewModel> Columns => _tablePresentation.Columns;

    /// <summary>Cells in column order — populated only when this section is an instance of a parent table.</summary>
    public IReadOnlyList<TableCellViewModel> Cells
    {
        get => _cells;
        private set { _cells = value; OnPropertyChanged(); }
    }

    public bool CanAddRow => IsDynamicTable && _nestedSections.Count < EffectiveMaxRows;

    // A table always keeps at least one instance: an "empty" table was never empty,
    // and an instance is what describes the table's own fields.
    public bool CanRemoveRow => IsDynamicTable && _nestedSections.Count > 1;

    private int EffectiveMaxRows =>
        int.TryParse(_section.MaxRows, out var max) && max > 0 ? max : int.MaxValue;

    public IRelayCommand AddRowCommand { get; }
    public IRelayCommand<SectionViewModel> RemoveRowCommand { get; }

    // ── Table authoring (edit mode) ──

    public IRelayCommand ConvertToFixedTableCommand { get; }
    public IRelayCommand ConvertToDynamicTableCommand { get; }
    public IRelayCommand RemoveTableLayoutCommand { get; }
    public IRelayCommand AddColumnCommand { get; }
    public IRelayCommand<TableColumnViewModel> RemoveColumnCommand { get; }
    public IRelayCommand AddFixedRowCommand { get; }
    public IRelayCommand<SectionViewModel> RemoveFixedRowCommand { get; }

    /// <summary>
    /// Runs a table mutation, recording it as a single undoable step. Every table edit
    /// reduces to "the section tree looked like X, now it looks like Y", so one command
    /// type covers converting, adding and removing columns, and adding and removing
    /// rows — there is no per-operation undo bookkeeping to keep in step.
    /// </summary>
    private void EditTable(Action mutate)
        => _tableMutation.Edit(mutate);

    public void ConvertToFixedTable() { if (!IsTableSection) EditTable(() => ApplyConvertToTable(canAddRows: false)); }

    public void ConvertToDynamicTable() { if (!IsTableSection) EditTable(() => ApplyConvertToTable(canAddRows: true)); }

    public void RemoveTableLayout() { if (IsTableSection) EditTable(ApplyRemoveTable); }

    public void AddColumn() { if (IsTableSection) EditTable(ApplyAddColumn); }

    public void RemoveColumn(TableColumnViewModel? column)
    {
        if (column is null || !IsTableSection) return;
        if (Columns.Count <= 1) return;
        EditTable(() => ApplyRemoveColumn(column.Index));
    }

    /// <summary>Retypes the prompt at this position in every instance.</summary>
    internal void RetypeColumn(int index, string type)
    {
        if (!IsTableSection) return;
        EditTable(() => _tableMutation.RetypeColumn(index, type));
    }

    /// <summary>Renames the prompt at this position in every instance.</summary>
    internal void RenameColumn(int index, string label)
    {
        if (!IsTableSection) return;
        EditTable(() => _tableMutation.RenameColumn(index, label));
    }

    public void AddFixedRow() { if (IsTableSection) EditTable(() => ApplyAddRow()); }

    public void RemoveFixedRow(SectionViewModel? row)
    {
        if (row is null || !IsTableSection) return;
        if (!_nestedSections.Contains(row)) return;
        if (_nestedSections.Count <= 1) return;
        EditTable(() => ApplyRemoveRow(row));
    }

    /// <summary>Append an instance at fill time. Not part of the authoring history.</summary>
    public void AddRow()
    {
        if (!IsDynamicTable || !CanAddRow) return;
        ApplyAddRow();
    }

    public void RemoveRow(SectionViewModel? row)
    {
        if (row is null || !IsDynamicTable || !CanRemoveRow) return;
        if (!_nestedSections.Contains(row)) return;
        ApplyRemoveRow(row);
    }

    // ── Apply helpers ──

    internal void ApplyConvertToTable(bool canAddRows)
        => _tableMutation.ConvertToTable(canAddRows);

    internal void ApplyRemoveTable()
        => _tableMutation.RemoveTable();

    internal void ApplyAddColumn()
        => _tableMutation.AddColumn();

    internal void ApplyRemoveColumn(int index)
        => _tableMutation.RemoveColumn(index);

    internal SectionViewModel ApplyAddRow()
        => _tableMutation.AddRow();

    internal void ApplyRemoveRow(SectionViewModel row)
        => _tableMutation.RemoveRow(row);

    // ── Snapshot / restore (the whole table undo story) ──

    internal TableSnapshot SnapshotTableState() => _tableMutation.Snapshot();

    internal void RestoreTableSnapshot(TableSnapshot snap)
        => _tableMutation.Restore(snap);

    /// <summary>Builds this section's cells, when it is an instance of a parent table.</summary>
    internal void ConfigureAsTableRow()
    {
        Cells = _promptViewModels.Select(vm => new TableCellViewModel(vm)).ToList();
    }

    internal void NotifyTableShapeChanged()
    {
        _tablePresentation.Refresh();
        OnPropertyChanged(nameof(IsTableSection));
        OnPropertyChanged(nameof(IsRegularSection));
        OnPropertyChanged(nameof(IsFixedTable));
        OnPropertyChanged(nameof(IsDynamicTable));
        OnPropertyChanged(nameof(Columns));
        OnPropertyChanged(nameof(MaxRowsText));
        NotifyTableMembershipChanged();
    }

    private void NotifyTableMembershipChanged()
    {
        OnPropertyChanged(nameof(NestedSections));
        OnPropertyChanged(nameof(CanAddRow));
        OnPropertyChanged(nameof(CanRemoveRow));
    }

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

    /// <summary>Creates a child VM using this section's shared editing services.</summary>
    internal SectionViewModel CreateChildSectionViewModel(Section section) =>
        new(section, _factory, _depth + 1, _onPromptAdded, _onPromptRemoved, _history);

    internal PromptViewModelBase CreatePromptViewModel(Prompt prompt) => _factory.Create(prompt);

    internal void NotifyPromptAdded(PromptViewModelBase prompt) => _onPromptAdded?.Invoke(prompt);

    internal void NotifyPromptRemoved(PromptViewModelBase prompt) => _onPromptRemoved?.Invoke(prompt);
}
