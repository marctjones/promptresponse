using CommunityToolkit.Mvvm.ComponentModel;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// One column of a table section, for the authoring UI.
/// </summary>
/// <remarks>
/// A column has no independent existence in the format: it *is* the prompt at a
/// given position, repeated across every instance. So this view-model holds only a
/// position, and editing the header writes the new label through to that position in
/// every row. There is no column record to fall out of step with its cells, because
/// there is no column record.
/// </remarks>
public sealed partial class TableColumnViewModel : ObservableObject
{
    private readonly SectionViewModel _table;

    internal TableColumnViewModel(SectionViewModel table, int index, string label, string? type, string? helpText)
    {
        _table = table;
        Index = index;
        _label = label;
        _type = type ?? "text";
        HelpText = helpText;
    }

    /// <summary>Position of this column within each instance's prompt list.</summary>
    public int Index { get; }

    private string _label;

    /// <summary>
    /// The column header. Setting it renames the corresponding prompt in every
    /// instance, because they are all names for the same field.
    /// </summary>
    public string Label
    {
        get => _label;
        set
        {
            var v = value ?? string.Empty;
            if (_label == v) return;
            _label = v;
            _table.RenameColumn(Index, v);
            OnPropertyChanged();
        }
    }

    private string _type;

    /// <summary>
    /// The column's type hint, which is the corresponding prompt's
    /// <c>expectedDataType</c> — advisory, like every hint.
    /// </summary>
    public string Type
    {
        get => _type;
        set
        {
            var v = string.IsNullOrWhiteSpace(value) ? "text" : value;
            if (_type == v) return;
            _type = v;
            _table.RetypeColumn(Index, v);
            OnPropertyChanged();
        }
    }

    /// <summary>Help text for the column, taken from the corresponding prompt.</summary>
    public string? HelpText { get; }
}
