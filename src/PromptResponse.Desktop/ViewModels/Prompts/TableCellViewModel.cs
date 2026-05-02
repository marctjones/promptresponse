using CommunityToolkit.Mvvm.ComponentModel;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// One cell in a <see cref="TablePromptViewModel"/>'s row × column grid. The value
/// is always a string per the vision invariant — type hints are advisory only,
/// any text is a valid cell response.
/// </summary>
public sealed partial class TableCellViewModel : ObservableObject
{
    public TableCellViewModel(string columnId, string columnLabel, string columnType, string? placeholder)
    {
        ColumnId = columnId;
        ColumnLabel = columnLabel;
        ColumnType = columnType;
        Placeholder = placeholder;
    }

    /// <summary>Stable column identifier — keys the cell in the persisted JSON row object.</summary>
    public string ColumnId { get; }

    /// <summary>User-visible column header label.</summary>
    public string ColumnLabel { get; }

    /// <summary>Type-hint for the column ("text", "number", "currency", "date", "boolean", ...).
    /// Used by validators / advisors to flag suspicious cell content; never enforced.</summary>
    public string ColumnType { get; }

    /// <summary>Optional placeholder text for empty cells.</summary>
    public string? Placeholder { get; }

    /// <summary>The cell's current value — always a string. Free text accepted.</summary>
    [ObservableProperty]
    private string _value = string.Empty;
}
