using PromptResponse.Core.Models;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.ViewModels.Editing;

/// <summary>
/// Applies model-backed table edits and coordinates their snapshot-based undo/redo.
/// </summary>
/// <remarks>
/// A table is represented by an ordinary section tree: child sections are rows and
/// prompts at a shared position are columns. This coordinator keeps that multi-row
/// mutation invariant in one place. <see cref="SectionViewModel"/> retains commands,
/// bindings, and host callbacks; <see cref="TablePresentationSynchronizer"/> owns only
/// derived column and cell projections.
/// </remarks>
internal sealed class TableMutationCoordinator
{
    private readonly SectionViewModel _owner;

    internal TableMutationCoordinator(SectionViewModel owner) => _owner = owner;

    private Section Table => _owner.Model;

    internal void Edit(Action mutate)
    {
        if (_owner.History is { IsApplying: false } history)
        {
            var before = Snapshot();
            mutate();
            history.Execute(new TableEditCommand(_owner, before, Snapshot()));
            return;
        }

        mutate();
    }

    internal TableSnapshot Snapshot() => new(
        Table.Kind,
        Table.CanAddRows,
        Table.MaxRows,
        Table.Sections.Select(TableSnapshotCloner.CloneSection).ToList(),
        Table.Prompts.Select(TableSnapshotCloner.ClonePrompt).ToList());

    internal void Restore(TableSnapshot snapshot)
    {
        foreach (var row in _owner.NestedSections.ToList())
        {
            NotifyRemoved(row.PromptViewModels);
        }
        NotifyRemoved(_owner.PromptViewModels);
        Table.Sections.Clear();
        Table.Prompts.Clear();
        _owner.NestedSections.Clear();
        _owner.PromptViewModels.Clear();

        Table.Kind = snapshot.Kind;
        Table.CanAddRows = snapshot.CanAddRows;
        Table.MaxRows = snapshot.MaxRows;

        foreach (var prompt in snapshot.DirectPrompts.Select(TableSnapshotCloner.ClonePrompt))
        {
            Table.Prompts.Add(prompt);
            var viewModel = CreatePromptViewModel(prompt);
            _owner.PromptViewModels.Add(viewModel);
            _owner.NotifyPromptAdded(viewModel);
        }
        foreach (var row in snapshot.Rows.Select(TableSnapshotCloner.CloneSection))
        {
            AttachRow(row);
        }
        _owner.NotifyTableShapeChanged();
    }

    internal void ConvertToTable(bool canAddRows)
    {
        // Any direct prompts become cells of the first table row, preserving data.
        var seedPrompts = new List<Prompt>(Table.Prompts);
        NotifyRemoved(_owner.PromptViewModels);
        Table.Prompts.Clear();
        _owner.PromptViewModels.Clear();

        Table.Kind = "table";
        Table.CanAddRows = canAddRows ? "true" : null;
        if (Table.Sections.Count == 0)
        {
            AttachRow(TableRowFactory.CreateFirstRow(Table.Id, seedPrompts));
        }
        _owner.NotifyTableShapeChanged();
    }

    internal void RemoveTable()
    {
        Table.Kind = null;
        Table.CanAddRows = null;
        Table.MaxRows = null;
        _owner.NotifyTableShapeChanged();
    }

    internal void AddColumn()
    {
        var ordinal = _owner.Columns.Count + 1;
        foreach (var row in _owner.NestedSections)
        {
            var cell = new Prompt
            {
                Id = $"{row.Model.Id}.col{ordinal}",
                Label = $"Column {ordinal}",
                Hints = new PromptHints { ExpectedDataType = "text" },
            };
            row.Model.Prompts.Add(cell);
            var viewModel = CreatePromptViewModel(cell);
            row.PromptViewModels.Add(viewModel);
            _owner.NotifyPromptAdded(viewModel);
        }
        _owner.NotifyTableShapeChanged();
    }

    internal void RemoveColumn(int index)
    {
        foreach (var row in _owner.NestedSections)
        {
            if (index < 0 || index >= row.Model.Prompts.Count) continue;
            var model = row.Model.Prompts[index];
            var viewModel = row.PromptViewModels.FirstOrDefault(vm => ReferenceEquals(vm.Model, model));
            if (viewModel != null)
            {
                _owner.NotifyPromptRemoved(viewModel);
                row.PromptViewModels.Remove(viewModel);
            }
            row.Model.Prompts.RemoveAt(index);
        }
        _owner.NotifyTableShapeChanged();
    }

    internal void MoveColumn(int fromIndex, int toIndex)
    {
        foreach (var row in _owner.NestedSections)
        {
            if (fromIndex >= row.Model.Prompts.Count || toIndex >= row.Model.Prompts.Count) continue;
            var prompt = row.Model.Prompts[fromIndex];
            row.Model.Prompts.RemoveAt(fromIndex);
            row.Model.Prompts.Insert(toIndex, prompt);

            var viewModel = row.PromptViewModels[fromIndex];
            row.PromptViewModels.RemoveAt(fromIndex);
            row.PromptViewModels.Insert(toIndex, viewModel);
        }
        _owner.NotifyTableShapeChanged();
    }

    internal void RetypeColumn(int index, string type)
    {
        foreach (var row in _owner.NestedSections)
        {
            if (index >= 0 && index < row.Model.Prompts.Count)
            {
                (row.Model.Prompts[index].Hints ??= new PromptHints()).ExpectedDataType = type;
            }
        }
        _owner.NotifyTableShapeChanged();
    }

    internal void RenameColumn(int index, string label)
    {
        foreach (var row in _owner.NestedSections)
        {
            if (index >= 0 && index < row.Model.Prompts.Count)
            {
                row.Model.Prompts[index].Label = label;
            }
        }
        _owner.NotifyTableShapeChanged();
    }

    internal SectionViewModel AddRow()
    {
        var firstRow = _owner.NestedSections.FirstOrDefault();
        var row = TableRowFactory.CreateRow(
            Table.Id,
            Table.Sections,
            firstRow?.Model.Prompts ?? [],
            TableRowFactory.TitlePrefix(firstRow?.Model));
        var rowViewModel = AttachRow(row);
        _owner.NotifyTableShapeChanged();
        return rowViewModel;
    }

    internal void RemoveRow(SectionViewModel row)
    {
        NotifyRemoved(row.PromptViewModels);
        Table.Sections.Remove(row.Model);
        _owner.NestedSections.Remove(row);
        RenumberGeneratedRowTitles();
        _owner.NotifyTableShapeChanged();
    }

    internal void MoveRow(int fromIndex, int toIndex)
    {
        var rowViewModel = _owner.NestedSections[fromIndex];
        _owner.NestedSections.RemoveAt(fromIndex);
        _owner.NestedSections.Insert(toIndex, rowViewModel);

        var row = Table.Sections[fromIndex];
        Table.Sections.RemoveAt(fromIndex);
        Table.Sections.Insert(toIndex, row);
        _owner.NotifyTableShapeChanged();
    }

    private SectionViewModel AttachRow(Section row)
    {
        Table.Sections.Add(row);
        var viewModel = _owner.CreateChildSectionViewModel(row);
        viewModel.ConfigureAsTableRow();
        _owner.NestedSections.Add(viewModel);
        NotifyAdded(viewModel.PromptViewModels);
        return viewModel;
    }

    private PromptViewModelBase CreatePromptViewModel(Prompt prompt) =>
        _owner.CreatePromptViewModel(prompt);

    private void RenumberGeneratedRowTitles()
    {
        var prefix = TableRowFactory.TitlePrefix(_owner.NestedSections.FirstOrDefault()?.Model);
        var ordinal = 1;
        foreach (var row in _owner.NestedSections)
        {
            var title = row.Model.Title?.TrimEnd() ?? string.Empty;
            var separator = title.LastIndexOf(' ');
            var isGenerated = separator > 0
                && title[..separator] == prefix
                && int.TryParse(title[(separator + 1)..], out _);
            if (isGenerated)
            {
                row.Model.Title = $"{prefix} {ordinal}";
                row.OnPropertyChanged(nameof(SectionViewModel.Title));
            }
            ordinal++;
        }
    }

    private void NotifyAdded(IEnumerable<PromptViewModelBase> prompts)
    {
        foreach (var prompt in prompts) _owner.NotifyPromptAdded(prompt);
    }

    private void NotifyRemoved(IEnumerable<PromptViewModelBase> prompts)
    {
        foreach (var prompt in prompts) _owner.NotifyPromptRemoved(prompt);
    }
}
