using System.Collections.ObjectModel;

namespace PromptResponse.Desktop.ViewModels.Editing;

/// <summary>
/// Rebuilds the presentation-only projections of a table section after its
/// model-backed shape changes.
/// </summary>
/// <remarks>
/// Tables deliberately have no separate column model: the first row's prompts
/// are the headers and every row's prompts are the cells. This object owns the
/// derived column collection and cell wrappers, while <see cref="SectionViewModel"/>
/// remains the owner of model mutation, undo history, and host callbacks.
/// </remarks>
internal sealed class TablePresentationSynchronizer
{
    private readonly SectionViewModel _table;
    private readonly ObservableCollection<SectionViewModel> _rows;

    internal TablePresentationSynchronizer(
        SectionViewModel table,
        ObservableCollection<SectionViewModel> rows)
    {
        _table = table;
        _rows = rows;
    }

    internal ObservableCollection<TableColumnViewModel> Columns { get; } = new();

    /// <summary>Synchronizes headers and every row's cell projection.</summary>
    internal void Refresh()
    {
        Columns.Clear();
        if (!_table.IsTableSection) return;

        var firstRowPrompts = _rows.FirstOrDefault()?.Model.Prompts;
        if (firstRowPrompts != null)
        {
            for (var index = 0; index < firstRowPrompts.Count; index++)
            {
                var prompt = firstRowPrompts[index];
                Columns.Add(new TableColumnViewModel(
                    _table,
                    index,
                    prompt.Label,
                    prompt.Hints?.ExpectedDataType,
                    prompt.Hints?.HelpText));
            }
        }

        foreach (var row in _rows)
        {
            row.ConfigureAsTableRow();
        }
    }
}
