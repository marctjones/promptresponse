using Excise.Core.Content;
using Excise.Core.Document;
using PdfeDoc = Excise.Core.Document.PdfDocument;

namespace PromptResponse.Conversion.Pdf;

/// <summary>A shape the page actually draws.</summary>
/// <param name="PageNumber">1-based page.</param>
/// <param name="Rect">Its extent in PDF user space.</param>
/// <param name="Kind">What was drawn.</param>
public sealed record Ruling(int PageNumber, PdfRectangle Rect, RulingKind Kind)
{
    /// <summary>Width in points.</summary>
    public double Width => Rect.Right - Rect.Left;

    /// <summary>Height in points.</summary>
    public double Height => Rect.Top - Rect.Bottom;
}

/// <summary>The kinds of drawn shape that matter to a form.</summary>
public enum RulingKind
{
    /// <summary>A stroked horizontal line — the rule a person writes on.</summary>
    HorizontalRule,

    /// <summary>A stroked vertical line — a column edge.</summary>
    VerticalRule,

    /// <summary>A stroked rectangle — a box, a tick box, or one cell of a comb.</summary>
    Box,
}

/// <summary>
/// Reads the lines and boxes a page draws.
/// </summary>
/// <remarks>
/// <para>
/// The blank a person writes on is not absence — it is a drawn primitive. On a
/// flattened form the widgets are gone but the ink is not, so the rules and
/// boxes are the only remaining evidence of where an answer goes. Measured on
/// <c>fed-w9-flat</c>, they line up with the source's widgets closely enough to
/// grade against: its eight tick boxes are drawn as 8×8 squares at exactly the
/// coordinates <c>fed-w9</c> declares.
/// </para>
/// <para>
/// <b>Only stroked paths count.</b> A filled rectangle is a grey header bar or a
/// rule of shading, not a blank; a clipping rectangle is never drawn at all.
/// Both must be excluded by the operator that <em>paints</em> the path rather
/// than by the path itself, which means grouping construction operators up to
/// the painting operator that consumes them. <c>fed-i9-flat</c> makes this
/// non-optional: it has 19 fills and 4 clipping paths on page 1 alone, where
/// <c>fed-w9-flat</c> has 2 fills and no clipping.
/// </para>
/// </remarks>
public static class PageRulings
{
    /// <summary>How far from axis-aligned a segment may be and still count as a rule, in points.</summary>
    public const double StraightnessTolerance = 0.5;

    /// <summary>The shortest segment worth reporting, in points.</summary>
    /// <remarks>
    /// Below this a "rule" is a tick, a serif on a drawn box, or rounding noise.
    /// <c>fed-i9-flat</c> draws a 0.8×1.0pt speck that is not anything.
    /// </remarks>
    public const double MinimumLength = 3.0;

    /// <summary>Reads every drawn rule and box on a page.</summary>
    public static IReadOnlyList<Ruling> Read(string path, int pageNumber)
    {
        using var doc = PdfeDoc.Open(path);
        return Read(doc.GetPage(pageNumber), pageNumber);
    }

    /// <summary>Reads every drawn rule and box on every page.</summary>
    public static IReadOnlyList<Ruling> ReadAll(string path)
    {
        using var doc = PdfeDoc.Open(path);
        return [.. Enumerable.Range(1, doc.Pages.Count).SelectMany(n => Read(doc.GetPage(n), n))];
    }

    private static List<Ruling> Read(PdfPage page, int number)
    {
        var rulings = new List<Ruling>();
        var pending = new List<ContentOperator>();
        ContentOperator? cursor = null;

        foreach (var op in page.GetContentStream().Operators)
        {
            switch (op.Category)
            {
                case OperatorCategory.PathConstruction:
                    pending.Add(op);
                    if (op.Name is "m" or "l")
                    {
                        cursor = op;
                    }

                    break;

                // A painting operator consumes everything constructed since the
                // last one. Only the stroking operators leave a visible line;
                // "W"/"n" clip and paint nothing at all.
                case OperatorCategory.PathPainting:
                case OperatorCategory.Clipping:
                    if (Strokes(op.Name))
                    {
                        rulings.AddRange(FromPath(pending, number));
                    }

                    pending.Clear();
                    cursor = null;
                    break;
            }

            _ = cursor;
        }

        return rulings;
    }

    /// <summary>Whether a painting operator draws the path's outline.</summary>
    private static bool Strokes(string name) => name is "S" or "s" or "B" or "B*" or "b" or "b*";

    private static IEnumerable<Ruling> FromPath(List<ContentOperator> path, int number)
    {
        (double X, double Y)? cursor = null;

        foreach (var op in path)
        {
            switch (op.Name)
            {
                case "re":
                {
                    var rect = Transform(op, op.GetNumber(0), op.GetNumber(1), op.GetNumber(2), op.GetNumber(3));
                    if (rect.Right - rect.Left >= MinimumLength || rect.Top - rect.Bottom >= MinimumLength)
                    {
                        yield return new Ruling(number, rect, RulingKind.Box);
                    }

                    break;
                }

                case "m":
                    cursor = Point(op, op.GetNumber(0), op.GetNumber(1));
                    break;

                case "l":
                {
                    var to = Point(op, op.GetNumber(0), op.GetNumber(1));
                    if (cursor is { } from)
                    {
                        var segment = AsRule(from, to, number);
                        if (segment is not null)
                        {
                            yield return segment;
                        }
                    }

                    cursor = to;
                    break;
                }

                // A curve ends the run of straight segments; a form's rules are
                // never curved, so the pen position is all that carries over.
                case "c" or "v" or "y":
                    cursor = null;
                    break;
            }
        }
    }

    private static Ruling? AsRule((double X, double Y) from, (double X, double Y) to, int number)
    {
        var dx = Math.Abs(to.X - from.X);
        var dy = Math.Abs(to.Y - from.Y);

        if (dy <= StraightnessTolerance && dx >= MinimumLength)
        {
            return new Ruling(
                number,
                new PdfRectangle(Math.Min(from.X, to.X), Math.Min(from.Y, to.Y), Math.Max(from.X, to.X), Math.Max(from.Y, to.Y)),
                RulingKind.HorizontalRule);
        }

        if (dx <= StraightnessTolerance && dy >= MinimumLength)
        {
            return new Ruling(
                number,
                new PdfRectangle(Math.Min(from.X, to.X), Math.Min(from.Y, to.Y), Math.Max(from.X, to.X), Math.Max(from.Y, to.Y)),
                RulingKind.VerticalRule);
        }

        return null;
    }

    private static (double X, double Y) Point(ContentOperator op, double x, double y) =>
        op.GraphicsTransform is { } t ? t.TransformPoint(x, y) : (x, y);

    private static PdfRectangle Transform(ContentOperator op, double x, double y, double w, double h)
    {
        var (x0, y0) = Point(op, x, y);
        var (x1, y1) = Point(op, x + w, y + h);
        return new PdfRectangle(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1));
    }
}
