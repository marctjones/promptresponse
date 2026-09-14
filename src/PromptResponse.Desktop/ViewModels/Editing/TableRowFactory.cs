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
                Role = cell.Role,
                Hints = NewRowHints(cell.Hints),
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

    /// <summary>Moves an existing prompt into a row, under the row's id.</summary>
    /// <remarks>
    /// The same prompt, renamed - so everything it carries travels with it, extension
    /// members included: a member present on read must still be present, unchanged, on
    /// write (specification 5.8, APR-MODEL-021). A copy rather than the prompt itself,
    /// and its own hints rather than the original's, so the row that results is not
    /// quietly sharing state with the section it was built from.
    /// </remarks>
    private static Prompt Rekey(Prompt prompt, string rowId)
    {
        var cell = ModelCopier.Copy(prompt);
        cell.Id = $"{rowId}.{SuffixOf(prompt.Id)}";
        return cell;
    }

    /// <summary>Gives a newly added row the column's shape, and none of its data.</summary>
    /// <remarks>
    /// A row is an instance of the table's shape, so a new one inherits the template
    /// cell's hints in full: type, placeholder, help text, suggested values, pattern,
    /// bounds, and expressions are what the column is. They are the specification's own
    /// members, and every expression binding a table cell can reach is either the cell
    /// itself (`_this`, `_id`) or something outside the row, since a dotted cell id is
    /// not a valid CEL identifier and gets no direct binding (specification 11.4).
    ///
    /// Extension members are not copied. They are the producer's own data about the
    /// object that carried them, and nothing tells this editor whether one describes
    /// the column or that particular cell - so putting `com.example.priority: 2` on a
    /// row the author has not filled in yet would be inventing their data, not
    /// preserving it. For an unprefixed member it would be worse than a guess: minting
    /// a name in the space reserved to the specification is exactly what a producer
    /// must not do (APR-MODEL-031), and the write path refuses it.
    ///
    /// Preservation is not in tension with this. It binds members that arrived on an
    /// object, and this object did not exist when the document was read.
    /// </remarks>
    private static PromptHints NewRowHints(PromptHints hints)
    {
        var shape = ModelCopier.Copy(hints);
        shape.Extensions = null;
        return shape;
    }

    private static string SuffixOf(string id)
    {
        var dot = id.LastIndexOf('.');
        return dot >= 0 ? id[(dot + 1)..] : id;
    }
}
