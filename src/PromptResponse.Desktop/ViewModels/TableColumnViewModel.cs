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

    internal TableColumnViewModel(SectionViewModel table, int index, string label)
    {
        _table = table;
        Index = index;
        _label = label;
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
}
