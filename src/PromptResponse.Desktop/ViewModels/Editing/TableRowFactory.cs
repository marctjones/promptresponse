using PromptResponse.Core.Models;

namespace PromptResponse.Desktop.ViewModels.Editing;

/// <summary>
/// Constructs the model objects for table instances. Table rows are ordinary
/// sections, but their ids, generated titles, and cell shape must stay aligned
/// with the parent table. Keeping that policy outside the view-model makes the
/// model-only part of table editing explicit and independently reusable.
/// </summary>
internal static class TableRowFactory
{
    internal static Section CreateFirstRow(string tableId, IReadOnlyList<Prompt> seedPrompts)
    {
        var rowId = NextRowId(tableId, []);
        var row = new Section { Id = rowId, Title = "Row 1" };
        row.Prompts.AddRange(seedPrompts.Count > 0
            ? seedPrompts.Select(prompt => Rekey(prompt, rowId))
            : [CreateDefaultCell(rowId)]);
        return row;
    }

    internal static Section CreateRow(
        string tableId,
        IReadOnlyList<Section> existingRows,
        IReadOnlyList<Prompt> shape,
        string titlePrefix)
    {
        var rowId = NextRowId(tableId, existingRows);
        var row = new Section { Id = rowId, Title = $"{titlePrefix} {existingRows.Count + 1}" };
        var cells = shape.Count > 0 ? shape : [new Prompt { Id = "col1", Label = "Column 1", Hints = new PromptHints { ExpectedDataType = "text" } }];

        foreach (var cell in cells)
        {
            row.Prompts.Add(new Prompt
            {
                Id = $"{rowId}.{SuffixOf(cell.Id)}",
                Label = cell.Label,
                Hints = CloneHints(cell.Hints),
            });
        }
        return row;
    }

    internal static string TitlePrefix(Section? firstRow)
    {
        var first = firstRow?.Title;
        if (string.IsNullOrWhiteSpace(first)) return "Row";
        var trimmed = first.TrimEnd();
        var cut = trimmed.LastIndexOf(' ');
        return cut > 0 && int.TryParse(trimmed[(cut + 1)..], out _) ? trimmed[..cut] : trimmed;
    }

    private static string NextRowId(string tableId, IReadOnlyList<Section> existingRows)
    {
        var n = existingRows.Count + 1;
        while (existingRows.Any(row => row.Id == $"{tableId}.row{n}")) n++;
        return $"{tableId}.row{n}";
    }

    private static Prompt CreateDefaultCell(string rowId) => new()
    {
        Id = $"{rowId}.col1",
        Label = "Column 1",
        Hints = new PromptHints { ExpectedDataType = "text" },
    };

    private static Prompt Rekey(Prompt prompt, string rowId) => new()
    {
        Id = $"{rowId}.{SuffixOf(prompt.Id)}",
        Label = prompt.Label,
        Response = prompt.Response,
        Hints = prompt.Hints,
    };

    private static PromptHints CloneHints(PromptHints hints) => new()
    {
        ExpectedDataType = hints.ExpectedDataType,
        Placeholder = hints.Placeholder,
        HelpText = hints.HelpText,
        SuggestedValues = new List<string>(hints.SuggestedValues),
    };

    private static string SuffixOf(string id)
    {
        var dot = id.LastIndexOf('.');
        return dot >= 0 ? id[(dot + 1)..] : id;
    }
}
