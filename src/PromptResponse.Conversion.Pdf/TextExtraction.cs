using Excise.Core.Document;
using Excise.Core.Text;
using PdfeDoc = Excise.Core.Document.PdfDocument;

namespace PromptResponse.Conversion.Pdf;

/// <summary>
/// Reads printed text as positioned runs, carrying the metrics later phases need
/// to tell a heading from a sentence.
/// </summary>
/// <remarks>
/// <para>
/// This does not reuse <c>PdfReferenceExtractor</c>, for two reasons. It needs a
/// per-span size metric the reference does not record; and the reference is the
/// answer key these phases are graded against, so extracting the converter's
/// input with the same code would make part of that grading circular.
/// </para>
/// <para>
/// <b>Size comes from the glyph box, not <c>Letter.FontSize</c>.</b> That
/// property is unreliable: every letter in <c>fed-w9</c> reports 1.0pt, because
/// the size lives in the text matrix rather than the <c>Tf</c> operand. The
/// rendered glyph rectangle is correct whichever way a producer wrote it, and
/// on the corpus it separates body text from headings cleanly — W-9's body is
/// 8pt with "Request for Taxpayer Identification Number and Certification" at
/// 14, I-9's body is 7pt with "Employment Eligibility Verification" at 14.
/// </para>
/// </remarks>
public static class TextExtraction
{
    /// <summary>Words on one baseline are one run.</summary>
    public const double BaselineTolerance = 2.0;

    /// <summary>A gap this many times the line's typical word gap ends the run.</summary>
    /// <remarks>
    /// Scaled to the lower quartile of the line's gaps — see
    /// <see cref="PromptResponse.Rendering.Pdf.PdfReferenceExtractor.ColumnGapMultiple"/>
    /// for why a median silently fails on exactly the rows that most need
    /// splitting, and why glyph height was tried and rejected.
    /// </remarks>
    public const double ColumnGapMultiple = 3.0;

    /// <summary>The smallest gap that may end the run, in points.</summary>
    public const double MinimumColumnGap = 8.0;

    /// <summary>A gap this many times the glyph height is a column break whatever else is on the line.</summary>
    /// <remarks>
    /// The ceiling on the relative rule. Swept against the label oracles: 3
    /// and 4 score 212 and 213 agreed, 6 and 8 both score 215, and no ceiling
    /// at all scores 213. Six is where it settles — tight enough to break
    /// SS-4's one-word caption cells, loose enough not to cut into the
    /// stretched word spacing of justified text, which is what a smaller cap
    /// does to fed-ss4's precision (94% down to 89% at a cap of 3).
    /// </remarks>
    public const double AbsoluteBreakMultiple = 6.0;

    /// <summary>Reads every page's text runs, with page-relative size metrics.</summary>
    public static IReadOnlyList<MeasuredSpan> Extract(string path)
    {
        using var doc = PdfeDoc.Open(path);
        var spans = new List<MeasuredSpan>();

        for (var page = 1; page <= doc.Pages.Count; page++)
        {
            var runs = RunsOnPage(doc.GetPage(page), page).ToList();
            if (runs.Count == 0)
            {
                continue;
            }

            // Body size is the page's modal run height rather than its mean:
            // a form is mostly body text, and a mean is dragged upward by a
            // handful of large headings — exactly the things being detected.
            var modal = runs
                .GroupBy(r => Math.Round(r.Height))
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .First().Key;

            spans.AddRange(runs.Select(r => r with { BodyHeight = modal <= 0 ? r.Height : modal }));
        }

        return spans;
    }

    private static IEnumerable<MeasuredSpan> RunsOnPage(PdfPage page, int number)
    {
        var words = page.GetWords().Where(w => !string.IsNullOrWhiteSpace(w.Text)).ToList();

        var lines = new List<List<Word>>();
        foreach (var word in words.OrderByDescending(w => w.BoundingBox.Bottom))
        {
            var line = lines.FirstOrDefault(l =>
                Math.Abs(l[0].BoundingBox.Bottom - word.BoundingBox.Bottom) <= BaselineTolerance);
            if (line is null)
            {
                lines.Add([word]);
            }
            else
            {
                line.Add(word);
            }
        }

        foreach (var line in lines)
        {
            foreach (var run in SplitAtColumnGaps([.. line.OrderBy(w => w.BoundingBox.Left)]))
            {
                var text = string.Join(" ", run.Select(w => w.Text.Trim()));
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                yield return new MeasuredSpan(
                    number,
                    text,
                    new PdfRectangle(
                        run.Min(w => w.BoundingBox.Left),
                        run.Min(w => w.BoundingBox.Bottom),
                        run.Max(w => w.BoundingBox.Right),
                        run.Max(w => w.BoundingBox.Top)),
                    run.Max(w => w.BoundingBox.Height),
                    0);
            }
        }
    }

    private static List<List<Word>> SplitAtColumnGaps(List<Word> inOrder)
    {
        if (inOrder.Count < 2)
        {
            return [inOrder];
        }

        var gaps = new double[inOrder.Count - 1];
        for (var i = 1; i < inOrder.Count; i++)
        {
            gaps[i - 1] = inOrder[i].BoundingBox.Left - inOrder[i - 1].BoundingBox.Right;
        }

        // Lower quartile, not median: column breaks are the large gaps, so a
        // statistic taken from the small end still estimates word spacing on a
        // line that is mostly column breaks. See ColumnGapMultiple.
        var sorted = gaps.Where(g => g > 0).Order().ToArray();
        var typical = sorted.Length == 0 ? 0 : sorted[sorted.Length / 4];

        // Capped at a few times the line's own glyph height, because the
        // quartile has nothing to work with when EVERY gap on the line is a
        // column break. SS-4's line 13 is three one-word captions --
        // "Agricultural", "Household", "Other" -- in three ruled cells, so both
        // its gaps are ~50pt, the quartile is 50, the threshold becomes 150 and
        // the row survives as one run. A 50pt gap in 8pt type is a column break
        // whatever the rest of the line looks like.
        var height = inOrder.Max(w => w.BoundingBox.Height);
        var threshold = Math.Min(
            Math.Max(MinimumColumnGap, typical * ColumnGapMultiple),
            Math.Max(MinimumColumnGap, height * AbsoluteBreakMultiple));

        var runs = new List<List<Word>>();
        runs.Add([inOrder[0]]);
        for (var i = 1; i < inOrder.Count; i++)
        {
            if (gaps[i - 1] > threshold)
            {
                runs.Add([]);
            }

            runs[^1].Add(inOrder[i]);
        }

        return runs;
    }
}

/// <summary>A run of text with the metrics needed to classify it.</summary>
/// <param name="PageNumber">1-based page.</param>
/// <param name="Text">The run's text.</param>
/// <param name="Rect">Its bounding box in PDF user space.</param>
/// <param name="Height">Tallest glyph box in the run — the usable size signal.</param>
/// <param name="BodyHeight">The modal run height on this page, i.e. body text size.</param>
/// <param name="HelpText">
/// The rest of the block after its opening question, or null when the block is
/// all one thing. See <see cref="TextBlocks.Split"/> — this is how a form's
/// guidance is kept and attached to the prompt it explains, rather than dropped.
/// </param>
public sealed record MeasuredSpan(
    int PageNumber,
    string Text,
    PdfRectangle Rect,
    double Height,
    double BodyHeight,
    string? HelpText = null)
{
    /// <summary>How large this run is relative to the page's body text.</summary>
    public double RelativeSize => BodyHeight <= 0 ? 1 : Height / BodyHeight;

    /// <summary>Words in the run.</summary>
    public int WordCount => Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
}
