using Pdfe.Core.Authoring;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;

namespace PromptResponse.Rendering.Pdf;

/// <summary>
/// Shared helpers for the PDF renderers (flat and fillable).
/// </summary>
internal static class PdfRenderHelpers
{
    /// <summary>
    /// Stamps the document's metadata onto the PDF Info dictionary (Title /
    /// Author / Subject) using pdfe v2.5.0's metadata authoring, for provenance
    /// and searchability.
    /// </summary>
    public static void ApplyMetadata(PdfDocumentBuilder pdf, Metadata meta)
    {
        if (!string.IsNullOrWhiteSpace(meta.Title))
        {
            pdf.Title(meta.Title);
        }

        // Prefer the document author; fall back to who filled it in.
        var author = !string.IsNullOrWhiteSpace(meta.Author) ? meta.Author : meta.FilledBy;
        if (!string.IsNullOrWhiteSpace(author))
        {
            pdf.Author(author!);
        }

        if (!string.IsNullOrWhiteSpace(meta.Description))
        {
            pdf.Subject(meta.Description!);
        }
    }

    /// <summary>
    /// Flattens a <see cref="TableBlock"/> into pdfe's row form: a header row
    /// (empty cell for the row-label column, then the column headers) followed
    /// by one row per <see cref="TableRowBlock"/> (label cell, then values).
    /// </summary>
    public static List<IReadOnlyList<string>> BuildTableRows(TableBlock table)
    {
        var rows = new List<IReadOnlyList<string>>(table.Rows.Count + 1);

        var header = new List<string>(table.ColumnHeaders.Count + 1) { string.Empty };
        header.AddRange(table.ColumnHeaders);
        rows.Add(header);

        foreach (var row in table.Rows)
        {
            var cells = new List<string>(row.Cells.Count + 1) { row.Label };
            cells.AddRange(row.Cells.Select(c => c.Value));
            rows.Add(cells);
        }

        return rows;
    }
}
