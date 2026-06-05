using Pdfe.Core.Document;
using Pdfe.Core.Graphics;

namespace PromptResponse.Rendering.Pdf;

/// <summary>
/// Minimal top-down flow layout over pdfe's coordinate-level graphics API:
/// tracks a cursor, word-wraps text to the content width, and starts a new page
/// when content overflows. Encapsulates the manual page/coordinate bookkeeping
/// that <c>Pdfe.Core</c> does not (yet) provide as a high-level API
/// (tracked upstream: marctjones/pdfe#379).
/// </summary>
/// <remarks>
/// PDF user space has its origin at the bottom-left with y increasing upward,
/// so the cursor <see cref="_y"/> is a y-coordinate that <em>decreases</em> as
/// we move down the page. Text is drawn at the baseline.
/// MVP scope: base-14 (Latin/WinAnsi) fonts only — Unicode font embedding is
/// tracked upstream (marctjones/pdfe#378).
/// </remarks>
internal sealed class PdfLayoutWriter : IDisposable
{
    private const double PageWidth = 612;   // US Letter, points
    private const double PageHeight = 792;
    private const double Margin = 54;        // 0.75"
    private const double ContentWidth = PageWidth - (2 * Margin);

    private static readonly PdfBrush TextBrush = PdfBrush.Black;
    private static readonly PdfBrush MutedBrush = new(PdfColor.FromGray(0.4));
    private static readonly PdfPen GridPen = new(PdfColor.FromGray(0.6), 0.5);

    private readonly PdfDocument _document;
    private PdfPage _page = null!;
    private PdfGraphics _graphics = null!;
    private double _y;

    public PdfLayoutWriter(PdfDocument document)
    {
        _document = document;
        NewPage();
    }

    /// <summary>Draws a paragraph of (optionally multi-line) text, word-wrapped to the content width.</summary>
    public void Paragraph(string text, PdfFont font, double indent = 0, double spacingAfter = 0, bool muted = false)
    {
        var brush = muted ? MutedBrush : TextBrush;
        var maxWidth = ContentWidth - indent;
        foreach (var line in WrapLines(text, font, maxWidth))
        {
            EnsureSpace(font.LineHeight);
            if (line.Length > 0)
            {
                _graphics.DrawString(line, font, brush, Margin + indent, _y - font.Ascender);
            }
            _y -= font.LineHeight;
        }
        _y -= spacingAfter;
    }

    /// <summary>Adds vertical space (e.g. between sections).</summary>
    public void Gap(double points)
    {
        _y -= points;
    }

    /// <summary>Draws a table: a header row followed by data rows, with a light grid.</summary>
    public void Table(IReadOnlyList<string> headers, IReadOnlyList<(string Label, IReadOnlyList<string> Cells)> rows, PdfFont headerFont, PdfFont cellFont)
    {
        // First column is the row label; remaining columns map to headers.
        var columnCount = headers.Count + 1;
        var colWidth = ContentWidth / columnCount;
        const double cellPad = 3;
        var cellTextWidth = colWidth - (2 * cellPad);

        var headerCells = new List<string> { string.Empty };
        headerCells.AddRange(headers);
        DrawRow(headerCells, headerFont, colWidth, cellPad, cellTextWidth);

        foreach (var (label, cells) in rows)
        {
            var rowCells = new List<string>(columnCount) { label };
            rowCells.AddRange(cells);
            DrawRow(rowCells, cellFont, colWidth, cellPad, cellTextWidth);
        }
        _y -= 6;
    }

    private void DrawRow(IReadOnlyList<string> cells, PdfFont font, double colWidth, double cellPad, double cellTextWidth)
    {
        // Wrap each cell, then size the row to the tallest cell.
        var wrapped = cells.Select(c => WrapLines(c, font, cellTextWidth)).ToList();
        var maxLines = Math.Max(1, wrapped.Max(w => w.Count));
        var rowHeight = (maxLines * font.LineHeight) + (2 * cellPad);

        EnsureSpace(rowHeight);
        var top = _y;

        for (var col = 0; col < cells.Count; col++)
        {
            var x = Margin + (col * colWidth);
            _graphics.DrawRectangle(x, top - rowHeight, colWidth, rowHeight, fill: null, stroke: GridPen);

            var lineY = top - cellPad;
            foreach (var line in wrapped[col])
            {
                if (line.Length > 0)
                {
                    _graphics.DrawString(line, font, TextBrush, x + cellPad, lineY - font.Ascender);
                }
                lineY -= font.LineHeight;
            }
        }

        _y -= rowHeight;
    }

    private void EnsureSpace(double height)
    {
        if (_y - height < Margin)
        {
            NewPage();
        }
    }

    private void NewPage()
    {
        if (_graphics is not null)
        {
            _graphics.Flush();
            _graphics.Dispose();
        }
        _page = _document.Pages.AddBlank(PageWidth, PageHeight);
        _graphics = _page.GetGraphics();
        _y = PageHeight - Margin;
    }

    /// <summary>Greedy word-wrap. Honors embedded newlines and hard-breaks words wider than the column.</summary>
    private static List<string> WrapLines(string text, PdfFont font, double maxWidth)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            lines.Add(string.Empty);
            return lines;
        }

        foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            var words = rawLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            var current = string.Empty;
            foreach (var word in words)
            {
                var candidate = current.Length == 0 ? word : current + " " + word;
                if (font.MeasureWidth(candidate) <= maxWidth || current.Length == 0)
                {
                    // A single word wider than the column is hard-broken by character.
                    if (current.Length == 0 && font.MeasureWidth(word) > maxWidth)
                    {
                        foreach (var piece in HardBreak(word, font, maxWidth))
                        {
                            lines.Add(piece);
                        }
                        current = string.Empty;
                        continue;
                    }
                    current = candidate;
                }
                else
                {
                    lines.Add(current);
                    current = word;
                }
            }
            if (current.Length > 0)
            {
                lines.Add(current);
            }
        }
        return lines;
    }

    private static IEnumerable<string> HardBreak(string word, PdfFont font, double maxWidth)
    {
        var piece = string.Empty;
        foreach (var ch in word)
        {
            var candidate = piece + ch;
            if (piece.Length > 0 && font.MeasureWidth(candidate) > maxWidth)
            {
                yield return piece;
                piece = ch.ToString();
            }
            else
            {
                piece = candidate;
            }
        }
        if (piece.Length > 0)
        {
            yield return piece;
        }
    }

    public void Dispose()
    {
        if (_graphics is not null)
        {
            _graphics.Flush();
            _graphics.Dispose();
            _graphics = null!;
        }
    }
}
