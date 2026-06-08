using Pdfe.Core.Authoring;
using Pdfe.Core.Document;
using Pdfe.Core.Graphics;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;

namespace PromptResponse.Rendering.Pdf;

/// <summary>
/// Shared helpers for the PDF renderers (flat and fillable).
/// </summary>
internal static class PdfRenderHelpers
{
    /// <summary>Maps the renderer's page-size option to a pdfe <see cref="PageSize"/>.</summary>
    public static PageSize ToPageSize(PdfPageSize size) => size switch
    {
        PdfPageSize.A4 => PageSize.A4,
        PdfPageSize.Legal => PageSize.Legal,
        _ => PageSize.Letter,
    };

    /// <summary>
    /// Draws running page elements on every page of an already-built document:
    /// an optional classification/handling banner (centered, bold, top and
    /// bottom) and a footer (label left, generated date centre, "Page X of Y"
    /// right, above a thin rule). Runs after layout (so the page total is known),
    /// appends to each page's content, and sits inside the margins so nothing
    /// overlaps the form body.
    /// </summary>
    public static void ApplyRunningElements(PdfDocument doc, PdfRenderOptions options, string title)
    {
        var banner = string.IsNullOrWhiteSpace(options.BannerText) ? null : options.BannerText!.Trim();
        if (!options.ShowFooter && banner is null)
        {
            return;
        }

        var font = PdfFont.Helvetica(8);
        var bannerFont = PdfFont.HelveticaBold(9);
        var brush = PdfBrush.Black;
        var pen = new PdfPen(PdfColor.FromGray(0.75), 0.5);

        var label = string.IsNullOrWhiteSpace(options.FooterLabel) ? title : options.FooterLabel!;
        var date = options.ShowGeneratedDate
            ? options.GeneratedOn ?? DateTime.Now.ToString("yyyy-MM-dd")
            : null;

        var total = doc.PageCount;
        const double margin = 72, footerY = 30, ruleY = 46, bottomBannerY = 14;

        for (var i = 1; i <= total; i++)
        {
            var page = doc.GetPage(i);
            var g = page.GetGraphics();
            double left = margin, right = page.Width - margin, centre = page.Width / 2;

            if (banner is not null)
            {
                // Top and bottom handling markings, in the page margins.
                g.DrawString(banner, bannerFont, brush, centre, page.Height - 30, TextAlignment.Center);
                g.DrawString(banner, bannerFont, brush, centre, bottomBannerY, TextAlignment.Center);
            }

            if (options.ShowFooter)
            {
                g.DrawLine(left, ruleY, right, ruleY, pen);
                if (!string.IsNullOrWhiteSpace(label))
                {
                    g.DrawString(Truncate(label, 70), font, brush, left, footerY, TextAlignment.Left);
                }
                if (date != null)
                {
                    g.DrawString($"Generated {date}", font, brush, centre, footerY, TextAlignment.Center);
                }
                if (options.ShowPageNumbers)
                {
                    g.DrawString($"Page {i} of {total}", font, brush, right, footerY, TextAlignment.Right);
                }
            }

            g.Flush();
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "...";

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
