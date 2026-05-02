using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// One row in a <see cref="TablePromptViewModel"/>. Carries a row id (stable for
/// fixed-row tables, GUID-based for dynamic rows), a user-visible label, and a
/// cell per declared column.
/// </summary>
public sealed partial class TableRowViewModel : ObservableObject
{
    public TableRowViewModel(string id, string label, IEnumerable<TableCellViewModel> cells)
    {
        Id = id;
        Label = label;
        Cells = new ObservableCollection<TableCellViewModel>(cells);
    }

    /// <summary>Stable row id — for fixed rows, this is the FixedRow.Id. For dynamic
    /// rows, an auto-generated GUID. Used as the key in the persisted JSON object
    /// for fixed-row tables.</summary>
    public string Id { get; }

    /// <summary>User-visible row label — for fixed rows, the FixedRow.Label. For
    /// dynamic rows, "{RowLabel} {ordinal}" computed by the parent VM.</summary>
    [ObservableProperty]
    private string _label;

    /// <summary>Cells in column order. Indexed by parent's <see cref="TableDefinition.Columns"/>.</summary>
    public ObservableCollection<TableCellViewModel> Cells { get; }
}
