using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.Input;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Tabular prompt: rows × columns of values. The stored response is a JSON object
/// (fixed rows, keyed by row id) or array-of-objects (dynamic rows). Every leaf
/// is a string; type hints are advisory; any visible text is a valid cell value.
/// </summary>
public sealed class TablePromptViewModel : PromptViewModelBase
{
    private readonly TableDefinition? _definition;
    private bool _suppressSerialization;

    public TablePromptViewModel(Prompt prompt, IProfileService profileService)
        : base(prompt, profileService)
    {
        _definition = prompt.Hints.TableDefinition;
        Rows = new ObservableCollection<TableRowViewModel>();
        AddRowCommand = new RelayCommand(AddRow, () => CanAddRow);
        RemoveRowCommand = new RelayCommand<TableRowViewModel>(RemoveRow, _ => CanRemoveRow);

        if (_definition != null)
        {
            HydrateRowsFromResponse();
        }
    }

    public TableDefinition? Definition => _definition;
    public bool IsFixedTable => _definition?.IsFixedTable ?? false;
    public bool IsDynamicTable => _definition?.IsDynamicTable ?? false;
    public bool HasDefinition => _definition != null;

    /// <summary>Materialised rows for view binding — header label + per-column cells.</summary>
    public ObservableCollection<TableRowViewModel> Rows { get; }

    /// <summary>Columns to display, in declaration order. Empty when no definition.</summary>
    public IReadOnlyList<TableColumn> Columns => _definition?.Columns ?? new List<TableColumn>();

    /// <summary>Add-row button command — only enabled for dynamic tables under MaxRows.</summary>
    public IRelayCommand AddRowCommand { get; }

    /// <summary>Remove-row button command per row — only enabled for dynamic tables above MinRows.</summary>
    public IRelayCommand<TableRowViewModel> RemoveRowCommand { get; }

    public bool CanAddRow =>
        IsDynamicTable
        && _definition!.DynamicRows is { } d
        && Rows.Count < d.MaxRows;

    public bool CanRemoveRow =>
        IsDynamicTable
        && _definition!.DynamicRows is { } d
        && Rows.Count > d.MinRows
        && Rows.Count > 0;

    private void HydrateRowsFromResponse()
    {
        if (_definition == null) return;

        _suppressSerialization = true;
        try
        {
            Rows.Clear();
            JsonNode? parsed = null;
            if (!string.IsNullOrWhiteSpace(Response))
            {
                try { parsed = JsonNode.Parse(Response); }
                catch { /* free-text fallback — leave parsed null and ignore */ }
            }

            if (IsFixedTable)
            {
                var byRowId = parsed as JsonObject;
                foreach (var fixedRow in _definition.FixedRows!)
                {
                    var rowData = byRowId?[fixedRow.Id] as JsonObject;
                    var cells = _definition.Columns.Select(c => new TableCellViewModel(
                        c.Id, c.Label, c.Type, c.Placeholder)
                    {
                        Value = rowData?[c.Id]?.GetValue<string?>() ?? string.Empty,
                    });
                    AddRowToCollection(new TableRowViewModel(fixedRow.Id, fixedRow.Label, cells));
                }
            }
            else if (IsDynamicTable)
            {
                var rowsArray = parsed as JsonArray;
                if (rowsArray != null)
                {
                    var idx = 0;
                    foreach (var rowNode in rowsArray)
                    {
                        var rowData = rowNode as JsonObject;
                        var cells = _definition.Columns.Select(c => new TableCellViewModel(
                            c.Id, c.Label, c.Type, c.Placeholder)
                        {
                            Value = rowData?[c.Id]?.GetValue<string?>() ?? string.Empty,
                        });
                        var rowId = rowData?["__rowId"]?.GetValue<string?>() ?? Guid.NewGuid().ToString("N");
                        AddRowToCollection(new TableRowViewModel(rowId, $"{_definition.DynamicRows!.RowLabel} {++idx}", cells));
                    }
                }
                // Pad up to MinRows so dynamic tables open with the configured floor.
                while (Rows.Count < _definition.DynamicRows!.MinRows)
                {
                    AddRow();
                }
            }
        }
        finally
        {
            _suppressSerialization = false;
        }
        OnDerivedPropertiesShouldRefresh();
    }

    private void AddRowToCollection(TableRowViewModel row)
    {
        foreach (var cell in row.Cells)
        {
            cell.PropertyChanged += OnCellChanged;
        }
        Rows.Add(row);
    }

    private void OnCellChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_suppressSerialization) return;
        if (e.PropertyName == nameof(TableCellViewModel.Value))
        {
            SerializeRowsToResponse();
        }
    }

    /// <summary>Adds a fresh empty row at the end. Only meaningful for dynamic tables.</summary>
    public void AddRow()
    {
        if (_definition?.DynamicRows is null) return;
        if (Rows.Count >= _definition.DynamicRows.MaxRows) return;

        var ordinal = Rows.Count + 1;
        var cells = _definition.Columns.Select(c => new TableCellViewModel(
            c.Id, c.Label, c.Type, c.Placeholder));
        var row = new TableRowViewModel(
            Guid.NewGuid().ToString("N"),
            $"{_definition.DynamicRows.RowLabel} {ordinal}",
            cells);
        AddRowToCollection(row);
        SerializeRowsToResponse();
        AddRowCommand.NotifyCanExecuteChanged();
        RemoveRowCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Removes <paramref name="row"/> from a dynamic table. Renumbers labels.</summary>
    public void RemoveRow(TableRowViewModel? row)
    {
        if (row is null) return;
        if (_definition?.DynamicRows is null) return;
        if (Rows.Count <= _definition.DynamicRows.MinRows) return;

        foreach (var cell in row.Cells) cell.PropertyChanged -= OnCellChanged;
        Rows.Remove(row);
        // Re-label remaining rows so "Item 1, Item 2, …" stays contiguous.
        for (var i = 0; i < Rows.Count; i++)
        {
            Rows[i].Label = $"{_definition.DynamicRows.RowLabel} {i + 1}";
        }
        SerializeRowsToResponse();
        AddRowCommand.NotifyCanExecuteChanged();
        RemoveRowCommand.NotifyCanExecuteChanged();
    }

    private void SerializeRowsToResponse()
    {
        if (_definition == null) return;
        _suppressSerialization = true;
        try
        {
            string serialized;
            if (IsFixedTable)
            {
                var obj = new JsonObject();
                foreach (var row in Rows)
                {
                    var cells = new JsonObject();
                    foreach (var c in row.Cells) cells[c.ColumnId] = c.Value;
                    obj[row.Id] = cells;
                }
                serialized = obj.ToJsonString();
            }
            else if (IsDynamicTable)
            {
                var arr = new JsonArray();
                foreach (var row in Rows)
                {
                    var cells = new JsonObject { ["__rowId"] = row.Id };
                    foreach (var c in row.Cells) cells[c.ColumnId] = c.Value;
                    arr.Add(cells);
                }
                serialized = arr.ToJsonString();
            }
            else
            {
                return;
            }
            Response = serialized;
        }
        finally
        {
            _suppressSerialization = false;
        }
    }
}
