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
    private readonly ObservableCollection<TableColumnViewModel> _columnsObservable = new();
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
        if (_section.IsTable)
        {
            SyncColumnsObservable();
            foreach (var rowVm in _nestedSections)
            {
                rowVm.ConfigureAsTableRow();
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

    /// <summary>
    /// Reorder a column. A column is a position in every instance's prompt list, so
    /// reordering one moves that prompt in every row — the cells travel with their
    /// header, because they are the same thing. Undoable.
    /// </summary>
    public void MoveColumn(int fromIndex, int toIndex)
    {
        if (!IsTableSection || fromIndex == toIndex) return;
        var count = _columnsObservable.Count;
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
        foreach (var rowVm in _nestedSections)
        {
            var prompts = rowVm._section.Prompts;
            if (fromIndex >= prompts.Count || toIndex >= prompts.Count) continue;
            var cell = prompts[fromIndex];
            prompts.RemoveAt(fromIndex);
            prompts.Insert(toIndex, cell);

            var vm = rowVm._promptViewModels[fromIndex];
            rowVm._promptViewModels.RemoveAt(fromIndex);
            rowVm._promptViewModels.Insert(toIndex, vm);
        }
        NotifyTableShapeChanged();
    }

    internal void ApplyMoveFixedRow(int fromIndex, int toIndex)
    {
        var rowVm = _nestedSections[fromIndex];
        _nestedSections.RemoveAt(fromIndex);
        _nestedSections.Insert(toIndex, rowVm);

        var rowSec = _section.Sections[fromIndex];
        _section.Sections.RemoveAt(fromIndex);
        _section.Sections.Insert(toIndex, rowSec);
        NotifyTableShapeChanged();
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
    //
    // A table introduces no new primitive: rows are ordinary child sections and cells
    // are ordinary prompts. A "column" is simply the prompt at a given position,
    // repeated across every instance — so every table edit here is a mutation of the
    // section tree, and one snapshot-based undo covers all of them.

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
    public ObservableCollection<TableColumnViewModel> Columns => _columnsObservable;

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
    {
        if (_history != null && !_history.IsApplying)
        {
            var before = SnapshotTableState();
            mutate();
            var after = SnapshotTableState();
            _history.Execute(new TableEditCommand(this, before, after));
        }
        else
        {
            mutate();
        }
    }

    public void ConvertToFixedTable() { if (!IsTableSection) EditTable(() => ApplyConvertToTable(canAddRows: false)); }

    public void ConvertToDynamicTable() { if (!IsTableSection) EditTable(() => ApplyConvertToTable(canAddRows: true)); }

    public void RemoveTableLayout() { if (IsTableSection) EditTable(ApplyRemoveTable); }

    public void AddColumn() { if (IsTableSection) EditTable(ApplyAddColumn); }

    public void RemoveColumn(TableColumnViewModel? column)
    {
        if (column is null || !IsTableSection) return;
        if (_columnsObservable.Count <= 1) return;
        EditTable(() => ApplyRemoveColumn(column.Index));
    }

    /// <summary>Renames the prompt at this position in every instance.</summary>
    internal void RenameColumn(int index, string label)
    {
        if (!IsTableSection) return;
        EditTable(() =>
        {
            foreach (var rowVm in _nestedSections)
            {
                if (index >= 0 && index < rowVm._section.Prompts.Count)
                {
                    rowVm._section.Prompts[index].Label = label;
                }
            }
            ReconfigureAllRowsAsTableRows();
            NotifyTableShapeChanged();
        });
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
    {
        // A table's structure lives in its instances, so any free-floating prompts on
        // the section itself become the first instance's cells rather than being lost.
        var seedPrompts = new List<Prompt>(_section.Prompts);
        foreach (var p in _promptViewModels.ToList()) _onPromptRemoved?.Invoke(p);
        _section.Prompts.Clear();
        _promptViewModels.Clear();

        _section.Kind = "table";
        _section.CanAddRows = canAddRows ? "true" : null;

        if (_section.Sections.Count == 0)
        {
            var row = new Section { Id = NextRowId(), Title = "Row 1" };
            row.Prompts.AddRange(seedPrompts.Count > 0
                ? seedPrompts.Select(p => Rekey(p, row.Id))
                : [new Prompt { Id = $"{row.Id}.col1", Label = "Column 1", Hints = new PromptHints { ExpectedDataType = "text" } }]);
            AttachRow(row);
        }
        NotifyTableShapeChanged();
    }

    internal void ApplyRemoveTable()
    {
        _section.Kind = null;
        _section.CanAddRows = null;
        _section.MaxRows = null;
        NotifyTableShapeChanged();
    }

    internal void ApplyAddColumn()
    {
        var ordinal = _columnsObservable.Count + 1;
        foreach (var rowVm in _nestedSections)
        {
            var cell = new Prompt
            {
                Id = $"{rowVm._section.Id}.col{ordinal}",
                Label = $"Column {ordinal}",
                Hints = new PromptHints { ExpectedDataType = "text" },
            };
            rowVm._section.Prompts.Add(cell);
            var vm = _factory.Create(cell);
            rowVm._promptViewModels.Add(vm);
            _onPromptAdded?.Invoke(vm);
        }
        ReconfigureAllRowsAsTableRows();
        NotifyTableShapeChanged();
    }

    internal void ApplyRemoveColumn(int index)
    {
        foreach (var rowVm in _nestedSections)
        {
            if (index < 0 || index >= rowVm._section.Prompts.Count) continue;
            var model = rowVm._section.Prompts[index];
            var vm = rowVm._promptViewModels.FirstOrDefault(v => ReferenceEquals(v.Model, model));
            if (vm != null)
            {
                _onPromptRemoved?.Invoke(vm);
                rowVm._promptViewModels.Remove(vm);
            }
            rowVm._section.Prompts.RemoveAt(index);
        }
        ReconfigureAllRowsAsTableRows();
        NotifyTableShapeChanged();
    }

    internal SectionViewModel ApplyAddRow()
    {
        var rowId = NextRowId();
        var ordinal = _nestedSections.Count + 1;
        var row = new Section { Id = rowId, Title = $"{RowTitlePrefix()} {ordinal}" };

        // A new instance takes its shape from the ones already there — which is why no
        // separate column definition or row-label template is needed.
        var shape = _nestedSections.FirstOrDefault()?._section.Prompts ?? [];
        foreach (var cell in shape)
        {
            row.Prompts.Add(new Prompt
            {
                Id = $"{rowId}.{SuffixOf(cell.Id)}",
                Label = cell.Label,
                Hints = new PromptHints
                {
                    ExpectedDataType = cell.Hints.ExpectedDataType,
                    Placeholder = cell.Hints.Placeholder,
                    HelpText = cell.Hints.HelpText,
                    SuggestedValues = new List<string>(cell.Hints.SuggestedValues),
                },
            });
        }
        var rowVm = AttachRow(row);
        NotifyTableShapeChanged();
        return rowVm;
    }

    internal void ApplyRemoveRow(SectionViewModel row)
    {
        foreach (var p in row._promptViewModels) _onPromptRemoved?.Invoke(p);
        _section.Sections.Remove(row._section);
        _nestedSections.Remove(row);
        RenumberGeneratedRowTitles();
        NotifyTableShapeChanged();
    }

    /// <summary>
    /// Tidies auto-generated instance titles after a removal — but only those still
    /// matching the generated "&lt;prefix&gt; &lt;n&gt;" pattern. A title a person edited
    /// ("Consulting fees") is data, and renumbering must never overwrite it.
    /// </summary>
    private void RenumberGeneratedRowTitles()
    {
        var prefix = RowTitlePrefix();
        var ordinal = 1;
        foreach (var rowVm in _nestedSections)
        {
            var title = rowVm._section.Title?.TrimEnd() ?? string.Empty;
            var cut = title.LastIndexOf(' ');
            var isGenerated = cut > 0
                && title[..cut] == prefix
                && int.TryParse(title[(cut + 1)..], out _);
            if (isGenerated)
            {
                rowVm._section.Title = $"{prefix} {ordinal}";
                rowVm.OnPropertyChanged(nameof(Title));
            }
            ordinal++;
        }
    }

    private SectionViewModel AttachRow(Section row)
    {
        _section.Sections.Add(row);
        var rowVm = new SectionViewModel(row, _factory, _depth + 1, _onPromptAdded, _onPromptRemoved, _history);
        rowVm.ConfigureAsTableRow();
        _nestedSections.Add(rowVm);
        foreach (var p in rowVm._promptViewModels) _onPromptAdded?.Invoke(p);
        return rowVm;
    }

    private string NextRowId()
    {
        var n = _section.Sections.Count + 1;
        while (_section.Sections.Any(r => r.Id == $"row{n}")) n++;
        return $"row{n}";
    }

    /// <summary>The word existing instances are named with, so new ones match.</summary>
    private string RowTitlePrefix()
    {
        var first = _nestedSections.FirstOrDefault()?._section.Title;
        if (string.IsNullOrWhiteSpace(first)) return "Row";
        var trimmed = first.TrimEnd();
        var cut = trimmed.LastIndexOf(' ');
        return cut > 0 && int.TryParse(trimmed[(cut + 1)..], out _) ? trimmed[..cut] : trimmed;
    }

    private static string SuffixOf(string id)
    {
        var dot = id.LastIndexOf('.');
        return dot >= 0 ? id[(dot + 1)..] : id;
    }

    private static Prompt Rekey(Prompt p, string rowId) => new()
    {
        Id = $"{rowId}.{SuffixOf(p.Id)}",
        Label = p.Label,
        Response = p.Response,
        Hints = p.Hints,
    };

    // ── Snapshot / restore (the whole table undo story) ──

    internal TableSnapshot SnapshotTableState() => new(
        _section.Kind,
        _section.CanAddRows,
        _section.MaxRows,
        _section.Sections.Select(CloneSection).ToList(),
        _section.Prompts.Select(ClonePrompt).ToList());

    internal void RestoreTableSnapshot(TableSnapshot snap)
    {
        foreach (var rowVm in _nestedSections.ToList())
        {
            foreach (var p in rowVm._promptViewModels) _onPromptRemoved?.Invoke(p);
        }
        foreach (var p in _promptViewModels.ToList()) _onPromptRemoved?.Invoke(p);
        _section.Sections.Clear();
        _section.Prompts.Clear();
        _nestedSections.Clear();
        _promptViewModels.Clear();

        _section.Kind = snap.Kind;
        _section.CanAddRows = snap.CanAddRows;
        _section.MaxRows = snap.MaxRows;

        foreach (var prompt in snap.DirectPrompts.Select(ClonePrompt))
        {
            _section.Prompts.Add(prompt);
            var vm = _factory.Create(prompt);
            _promptViewModels.Add(vm);
            _onPromptAdded?.Invoke(vm);
        }
        foreach (var row in snap.Rows.Select(CloneSection))
        {
            AttachRow(row);
        }
        NotifyTableShapeChanged();
    }

    private static Section CloneSection(Section s) => new()
    {
        Id = s.Id,
        Title = s.Title,
        Description = s.Description,
        Kind = s.Kind,
        CanAddRows = s.CanAddRows,
        MaxRows = s.MaxRows,
        Prompts = s.Prompts.Select(ClonePrompt).ToList(),
        Sections = s.Sections.Select(CloneSection).ToList(),
    };

    private static Prompt ClonePrompt(Prompt p) => new()
    {
        Id = p.Id,
        Label = p.Label,
        Response = p.Response,
        Hints = new PromptHints
        {
            ExpectedDataType = p.Hints.ExpectedDataType,
            Placeholder = p.Hints.Placeholder,
            HelpText = p.Hints.HelpText,
            ValidationPattern = p.Hints.ValidationPattern,
            SuggestedValues = new List<string>(p.Hints.SuggestedValues),
            ExprHidden = p.Hints.ExprHidden,
            ExprValue = p.Hints.ExprValue,
            ExprExpected = p.Hints.ExprExpected,
            ExprValidation = p.Hints.ExprValidation,
            ExprReadOnly = p.Hints.ExprReadOnly,
        },
    };

    /// <summary>Builds this section's cells, when it is an instance of a parent table.</summary>
    internal void ConfigureAsTableRow()
    {
        Cells = _promptViewModels.Select(vm => new TableCellViewModel(vm)).ToList();
    }

    private void ReconfigureAllRowsAsTableRows()
    {
        if (!IsTableSection) return;
        foreach (var rowVm in _nestedSections) rowVm.ConfigureAsTableRow();
    }

    /// <summary>Re-derives the column headers from the first instance's prompts.</summary>
    private void SyncColumnsObservable()
    {
        _columnsObservable.Clear();
        if (!IsTableSection) return;
        var first = _nestedSections.FirstOrDefault()?._section.Prompts;
        if (first == null) return;
        for (var i = 0; i < first.Count; i++)
        {
            _columnsObservable.Add(new TableColumnViewModel(this, i, first[i].Label));
        }
    }

    private void NotifyTableShapeChanged()
    {
        SyncColumnsObservable();
        ReconfigureAllRowsAsTableRows();
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
}
