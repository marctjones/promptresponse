using Excise.Core.Document;

namespace PromptResponse.Conversion.Pdf;

/// <summary>
/// Turns the lines and boxes a page draws into the fields a person fills in.
/// </summary>
/// <remarks>
/// <para>
/// Three shapes account for the corpus, and they are distinguished by geometry
/// alone — no model, and no form-specific rules:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>A small square is a tick box.</b> On <c>fed-w9-flat</c> the eight drawn
/// 8×8 squares are the eight tick boxes its source declares, at the same
/// coordinates.
/// </description></item>
/// <item><description>
/// <b>Touching equal cells are one comb field.</b> W-9 draws its SSN as three
/// 14.4pt boxes per group — one per character — and emitting those as fields
/// would treble the count and place none of them.
/// </description></item>
/// <item><description>
/// <b>A horizontal rule with blank space above it is a write-on line.</b> This
/// covers both the ruled line and the boxed table cell; they differ in where
/// the label sits, which is <see cref="LabelRecovery"/>'s problem, not this
/// phase's.
/// </description></item>
/// </list>
/// <para>
/// <b>The blank above a rule does double duty.</b> It gives the emitted
/// rectangle a height — a rule is 1pt tall and a field is not — and it is the
/// filter that rejects rules which are not blanks at all. A form is full of
/// horizontal lines that are borders and underlines: measured on
/// <c>fed-w9-flat</c>, the page border at y=696 has 3.3pt of clear space under
/// the text above it, while the genuine blank at y=660 has 19.6pt.
/// </para>
/// </remarks>
public static class BlankDetector
{
    /// <summary>The largest side, in points, at which a box is a tick box rather than a field.</summary>
    /// <remarks>
    /// Corpus tick boxes are 8-12pt square; the smallest comb cell is 14.4pt
    /// wide and 24pt tall, and no write-on box is under 20pt wide.
    /// </remarks>
    public const double TickBoxMaximumSide = 13.0;

    /// <summary>How square a box must be to be a tick box.</summary>
    public const double TickBoxAspectTolerance = 1.3;

    /// <summary>How far apart two comb cells may sit and still be one field, in points.</summary>
    /// <remarks>
    /// Deliberately tight, and the reason matters: on W-9 the gap between SSN
    /// groups is exactly one cell wide (14.4pt), so any tolerance generous
    /// enough to be described as "about a cell" merges the whole row into one
    /// field and loses all five. Cells of one field touch.
    /// </remarks>
    public const double CombCellGap = 1.5;

    /// <summary>The least clear space above a rule for it to be a blank, in points.</summary>
    public const double MinimumBlankHeight = 8.0;

    /// <summary>The most height a blank is credited with, as a multiple of body text height.</summary>
    /// <remarks>
    /// Caps the box a rule under a heading or at the top of an empty region
    /// would otherwise claim. Corpus write-on fields are 10-38pt tall.
    /// </remarks>
    public const double MaximumBlankHeightMultiple = 4.0;

    /// <summary>How much a rule and the text above it must overlap horizontally to interact.</summary>
    public const double HorizontalOverlapTolerance = 1.0;

    /// <summary>Finds the fields a page draws but does not declare.</summary>
    /// <param name="rulings">Everything the page draws.</param>
    /// <param name="spans">The printed text, used to measure the blank above a rule.</param>
    public static IReadOnlyList<DiscoveredField> Detect(
        IReadOnlyList<Ruling> rulings,
        IReadOnlyList<MeasuredSpan> spans)
    {
        ArgumentNullException.ThrowIfNull(rulings);
        ArgumentNullException.ThrowIfNull(spans);

        var found = new List<DiscoveredField>();

        foreach (var page in rulings.GroupBy(r => r.PageNumber).OrderBy(g => g.Key))
        {
            var text = spans.Where(s => s.PageNumber == page.Key).ToList();
            var body = text.Count == 0 ? 8.0 : text[0].BodyHeight;

            var boxes = page.Where(r => r.Kind == RulingKind.Box).ToList();

            found.AddRange(boxes
                .Where(IsTickBox)
                .Select(b => Field(b.PageNumber, b.Rect, "boolean")));

            found.AddRange(Combs([.. boxes.Where(b => !IsTickBox(b))])
                .Select(r => Field(page.Key, r, "text")));

            found.AddRange(page
                .Where(r => r.Kind == RulingKind.HorizontalRule)
                .Select(r => AsBlank(r, text, body))
                .OfType<PdfRectangle>()
                .Select(r => Field(page.Key, r, "text")));
        }

        // A form draws the same rule twice often enough to matter -- a cell
        // border shared by the row above and below, or a box stroked over a
        // rule. Those are one blank, and emitting both would place two prompts
        // on one answer.
        var distinct = found
            .GroupBy(f => (
                f.PageNumber,
                Math.Round(f.TargetRect!.Value.Left, 1),
                Math.Round(f.TargetRect.Value.Bottom, 1),
                Math.Round(f.TargetRect.Value.Right, 1),
                Math.Round(f.TargetRect.Value.Top, 1)))
            .Select(g => g.First());

        // Ids are positional so a snapshot is stable: nothing about a flattened
        // form supplies a name, and a counter would renumber everything after a
        // field that was found or lost. Position alone is not unique though --
        // a rule and a box can share a corner -- so width disambiguates, and a
        // suffix covers what is left. An APR id must be unique or two prompts
        // collide and the document cannot round-trip.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return
        [
            .. distinct
                .OrderBy(f => f.PageNumber)
                .ThenByDescending(f => f.TargetRect!.Value.Top)
                .ThenBy(f => f.TargetRect!.Value.Left)
                .Select(f =>
                {
                    var r = f.TargetRect!.Value;
                    var id = $"p{f.PageNumber}-{r.Left:F0}-{r.Bottom:F0}-{r.Right - r.Left:F0}";
                    for (var n = 2; !seen.Add(id); n++)
                    {
                        id = $"p{f.PageNumber}-{r.Left:F0}-{r.Bottom:F0}-{r.Right - r.Left:F0}#{n}";
                    }

                    return f with { Id = id };
                }),
        ];
    }

    private static DiscoveredField Field(int page, PdfRectangle rect, string dataType) => new(
        Id: string.Empty,
        Label: null,
        LabelSource: LabelSource.None,
        Origin: FieldOrigin.TextLayer,
        PageNumber: page,
        TargetRect: rect,
        ExpectedDataType: dataType,
        Options: null,
        NeedsReview: [WhatAReviewerMustDecide]);

    /// <summary>
    /// What a later phase has to settle about a field found in the page's ink.
    /// </summary>
    /// <remarks>
    /// Two questions, not one, and the second is the one that is easy to
    /// forget. A drawn rule that no caption points at is often not a field at
    /// all: measured on <c>fed-i9-flat</c>, keeping only the candidates that
    /// acquired a label raises placement precision from 56% to 76% — but drops
    /// recall from 98% to 75%, because 30 real fields also failed to find one.
    /// <para>
    /// So the filter is deliberately NOT applied here. A missed field cannot be
    /// recovered by anything downstream, while a spurious one can still be
    /// pruned by something that reads the page. Saying so in the review queue
    /// is what lets that pruning happen where it is cheap.
    /// </para>
    /// </remarks>
    public const string WhatAReviewerMustDecide =
        "found from the page's ink, so it has no name and no certainty: needs a label, " +
        "and needs confirming that it is a field at all rather than a printed rule";

    private static bool IsTickBox(Ruling box)
    {
        if (box.Width > TickBoxMaximumSide || box.Height > TickBoxMaximumSide)
        {
            return false;
        }

        var longer = Math.Max(box.Width, box.Height);
        var shorter = Math.Min(box.Width, box.Height);
        return shorter > 0 && longer / shorter <= TickBoxAspectTolerance;
    }

    /// <summary>Merges touching, equally sized cells into the single field they spell.</summary>
    private static List<PdfRectangle> Combs(List<Ruling> boxes)
    {
        var merged = new List<PdfRectangle>();

        foreach (var row in boxes.GroupBy(b => (Math.Round(b.Rect.Bottom, 1), Math.Round(b.Rect.Top, 1))))
        {
            PdfRectangle? open = null;
            double cellWidth = 0;

            foreach (var cell in row.OrderBy(b => b.Rect.Left))
            {
                if (open is { } current
                    && Math.Abs(cell.Width - cellWidth) <= CombCellGap
                    && cell.Rect.Left - current.Right <= CombCellGap)
                {
                    open = new PdfRectangle(current.Left, current.Bottom, cell.Rect.Right, current.Top);
                    continue;
                }

                if (open is { } finished)
                {
                    merged.Add(finished);
                }

                open = cell.Rect;
                cellWidth = cell.Width;
            }

            if (open is { } last)
            {
                merged.Add(last);
            }
        }

        return merged;
    }

    /// <summary>
    /// Gives a horizontal rule the height of the clear space above it, or
    /// rejects it as not being a blank at all.
    /// </summary>
    private static PdfRectangle? AsBlank(Ruling rule, List<MeasuredSpan> text, double body)
    {
        // The nearest text sitting above this rule and over it.
        var ceiling = text
            .Where(s => s.Rect.Bottom >= rule.Rect.Top - HorizontalOverlapTolerance
                && s.Rect.Right > rule.Rect.Left + HorizontalOverlapTolerance
                && s.Rect.Left < rule.Rect.Right - HorizontalOverlapTolerance)
            .Select(s => s.Rect.Bottom)
            .DefaultIfEmpty(rule.Rect.Top + body * MaximumBlankHeightMultiple)
            .Min();

        var height = Math.Min(ceiling - rule.Rect.Top, body * MaximumBlankHeightMultiple);
        return height < MinimumBlankHeight
            ? null
            : new PdfRectangle(rule.Rect.Left, rule.Rect.Top, rule.Rect.Right, rule.Rect.Top + height);
    }
}
