using Excise.Core.Document;

namespace PromptResponse.Conversion.Pdf;

/// <summary>
/// Joins wrapped lines back into the block a person reads, and separates the
/// question at its head from the guidance that follows.
/// </summary>
/// <remarks>
/// <para>
/// Extraction yields one run per baseline, which is not the unit a form is
/// written in. W-9 prints its first question as a wrapped paragraph:
/// </para>
/// <code>
/// 1 Name of entity/individual. An entry is required. (For a sole proprietor or
/// disregarded entity, enter the owner's name on line 1, and enter the business
/// or disregarded entity's name on line 2.)
/// </code>
/// <para>
/// Left as separate lines this is unusable in both directions. The first line
/// reads as a sentence and is ruled out as an instruction, so the real question
/// never reaches pairing; meanwhile the trailing fragment "entity's name on line
/// 2.)" is short, survives classification, and sits <em>closer</em> to the field
/// than the line that actually asks the question — so the field gets labelled
/// with the tail of a parenthesis. That is a measured result, not a worry.
/// </para>
/// <para>
/// Assembling the block fixes both, and the head/tail split then does the rest:
/// the question is the first sentence and the remainder is help text. This is
/// also how instructions are <em>preserved</em> rather than discarded — the tail
/// travels with the prompt it belongs to instead of being dropped as prose.
/// </para>
/// </remarks>
public static class TextBlocks
{
    /// <summary>How far apart two lines may sit and still be one block, as a multiple of line height.</summary>
    /// <remarks>
    /// Consecutive lines of a paragraph are typically separated by a fraction of
    /// their own height; a new block starts a full line or more away. Measured on
    /// W-9, a wrapped continuation sits 1.4pt below its predecessor against a 7pt
    /// body, while the next question is 20.6pt away.
    /// </remarks>
    public const double LineGapMultiple = 1.5;

    /// <summary>How far a continuation's left edge may sit outside its block's, in points.</summary>
    /// <remarks>
    /// A continuation is flush with its block or indented under it — never to the
    /// left of it, which would mean a new, less-indented block.
    /// </remarks>
    public const double IndentTolerance = 2.0;

    /// <summary>How far a continuation may be indented past its block, as a multiple of line height.</summary>
    /// <remarks>
    /// "Indented under" needs a bound, or a block runs on into whatever follows
    /// it. Measured on W-9: a genuine hanging indent is 12pt against a 7pt body
    /// (1.7×), while the jump from the masthead "Department of the Treasury
    /// Internal Revenue Service" to question 1 is 25.6pt — a new block that an
    /// unbounded rule swallowed, giving field 1 the label "Internal Revenue
    /// Service Before you begin."
    /// </remarks>
    public const double IndentMultiple = 3.0;

    /// <summary>How close a field must sit to a run's left for the run to be its caption, in points.</summary>
    /// <remarks>
    /// W-9's tick boxes sit 5-6pt to the left of the text naming them.
    /// </remarks>
    public const double CaptionGap = 12.0;

    /// <summary>How far a line's spacing may differ from the block's established pitch, in points.</summary>
    /// <remarks>
    /// Once a block has two lines it has a leading of its own, and a third line
    /// has to match it. A paragraph does not change its line spacing halfway
    /// through, so a bigger step is a different block that merely fell inside
    /// the generous fixed limit.
    /// <para>
    /// This is the single most valuable rule in the phase. Measured against the
    /// label oracles it is worth 13 fields on its own — fed-i9 goes from 73% to
    /// 83% recall — where every other block rule tried was worth one or two.
    /// </para>
    /// </remarks>
    public const double PitchTolerance = 2.0;

    /// <summary>Characters a form uses to open a bulleted or arrowed item.</summary>
    public const string ItemMarkers = "\u25b6\u2022\u2023\u25aa\u25cf\u2219\u00bb";

    /// <summary>Assembles runs into blocks, keeping fields as block boundaries.</summary>
    /// <param name="runs">One run per baseline, as extraction produced them.</param>
    /// <param name="fields">
    /// Where the fields are. A field lying between two lines ends the block: text
    /// above a blank and text below it are answering to different questions, and
    /// merging across one is how a label swallows its neighbour's.
    /// </param>
    public static IReadOnlyList<MeasuredSpan> Assemble(
        IReadOnlyList<MeasuredSpan> runs,
        IReadOnlyList<(int Page, PdfRectangle Rect)> fields)
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(fields);

        var blocks = new List<MeasuredSpan>();

        foreach (var page in runs.GroupBy(r => r.PageNumber))
        {
            var onPage = fields.Where(f => f.Page == page.Key).Select(f => f.Rect).ToList();

            // Several blocks are open at once, because a page is not one column.
            // Scanning top-down with a single open block breaks every multi-column
            // layout: the next run down the page belongs to the other column, fails
            // to continue this block, and closes it -- which is how W-9's
            // "Exemption from Foreign Account Tax Compliance Act (FATCA) reporting
            // code (if any)" lost everything after its first line.
            var open = new List<MeasuredSpan>();
            var pitch = new List<double?>();

            foreach (var run in page.OrderByDescending(r => r.Rect.Top).ThenBy(r => r.Rect.Left))
            {
                // A block the scan has passed can never be continued, since runs
                // only ever arrive further down the page.
                for (var i = open.Count - 1; i >= 0; i--)
                {
                    if (open[i].Rect.Bottom - run.Rect.Top > open[i].Height * LineGapMultiple)
                    {
                        blocks.Add(open[i]);
                        open.RemoveAt(i);
                        pitch.RemoveAt(i);
                    }
                }

                var at = -1;
                for (var i = 0; i < open.Count; i++)
                {
                    if (Continues(open[i], run, onPage, pitch[i]))
                    {
                        at = i;
                        break;
                    }
                }

                if (at >= 0)
                {
                    pitch[at] = open[at].Rect.Bottom - run.Rect.Top;
                    open[at] = Join(open[at], run);
                }
                else
                {
                    open.Add(run);
                    pitch.Add(null);
                }
            }

            blocks.AddRange(open);
        }

        return
        [
            .. blocks
                .Select(Split)
                .OrderBy(b => b.PageNumber)
                .ThenByDescending(b => b.Rect.Top)
                .ThenBy(b => b.Rect.Left),
        ];
    }

    /// <summary>Whether a run is the continuation of the block above it.</summary>
    private static bool Continues(MeasuredSpan block, MeasuredSpan next, List<PdfRectangle> fields, double? pitch)
    {
        var gap = block.Rect.Bottom - next.Rect.Top;
        if (gap < -IndentTolerance || gap > block.Height * LineGapMultiple)
        {
            return false;
        }

        // The block's own leading, once it has one. See PitchTolerance.
        if (pitch is { } established
            && Math.Abs(gap - established) > PitchTolerance)
        {
            return false;
        }

        // A bullet or an arrow opens an item exactly as a number does. 8822
        // writes its sub-conditions as "> If your last return was a joint
        // return...", directly under the caption of the tick box above, and
        // merging the two buried both. Worth one field on its own.
        if (next.Text.TrimStart().Length > 0
            && ItemMarkers.Contains(next.Text.TrimStart()[0]))
        {
            return false;
        }

        // Flush with the block or indented under it, and starting within it --
        // but only so far indented. See IndentMultiple.
        if (next.Rect.Left < block.Rect.Left - IndentTolerance || next.Rect.Left >= block.Rect.Right)
        {
            return false;
        }

        if (next.Rect.Left - block.Rect.Left > block.Height * IndentMultiple)
        {
            return false;
        }

        // A numbered lead-in starts an item; it never continues one. This is the
        // boundary the page draws and the extractor cannot see -- W-9, W-4, SS-4
        // and 8822 all number their questions, and without it the masthead runs
        // straight on into question 1.
        if (StartsNewItem(next.Text))
        {
            return false;
        }

        // Same size of type. A caption and the heading above it can be adjacent
        // and aligned without being one block.
        if (Math.Max(block.Height, next.Height) > Math.Min(block.Height, next.Height) * 1.5)
        {
            return false;
        }

        // A run with a field immediately to its left, on its own line, is that
        // field's caption and starts a block of its own. Without this, W-9's
        // left-hand tick-box captions chain upward into the instruction above
        // them: "Individual/sole proprietor", "LLC. Enter the tax
        // classification..." and "Other (see instructions)" merged into a
        // single run, which was then ruled out as an instruction and cost all
        // three boxes their labels at once.
        if (fields.Any(f =>
                f.Top > next.Rect.Bottom + IndentTolerance
                && f.Bottom < next.Rect.Top - IndentTolerance
                && f.Right <= next.Rect.Left + IndentTolerance
                && next.Rect.Left - f.Right <= CaptionGap))
        {
            return false;
        }

        // A field between the two lines ends the block.
        return !fields.Any(f =>
            f.Top <= block.Rect.Bottom + IndentTolerance
            && f.Bottom >= next.Rect.Top - IndentTolerance
            && f.Right > next.Rect.Left && f.Left < block.Rect.Right);
    }

    /// <summary>Whether a run opens a numbered item, like "1 ", "3a " or "10b ".</summary>
    private static bool StartsNewItem(string text)
    {
        var trimmed = text.AsSpan().TrimStart();
        var i = 0;
        while (i < trimmed.Length && char.IsAsciiDigit(trimmed[i]))
        {
            i++;
        }

        if (i == 0)
        {
            return false;
        }

        if (i < trimmed.Length && char.IsAsciiLetterLower(trimmed[i]))
        {
            i++;
        }

        // A number followed by more number-ish text ("2024 tax year") is not an
        // item lead-in; a number followed by a space and a word is.
        return i < trimmed.Length && trimmed[i] is ' ' or '.';
    }

    private static MeasuredSpan Join(MeasuredSpan block, MeasuredSpan next) => block with
    {
        Text = $"{block.Text} {next.Text}",
        Rect = new PdfRectangle(
            Math.Min(block.Rect.Left, next.Rect.Left),
            Math.Min(block.Rect.Bottom, next.Rect.Bottom),
            Math.Max(block.Rect.Right, next.Rect.Right),
            Math.Max(block.Rect.Top, next.Rect.Top)),
        Height = Math.Max(block.Height, next.Height),
    };

    /// <summary>
    /// Splits a block into the question it opens with and the guidance that
    /// follows.
    /// </summary>
    /// <remarks>
    /// The break is the first sentence end followed by a capital: a form asks
    /// its question and then explains it. Requiring the word before the full
    /// stop to carry at least two letters keeps "U.S." and initials intact, and
    /// requiring a capital after it keeps "Rev. 3-2024" whole.
    /// </remarks>
    public static MeasuredSpan Split(MeasuredSpan block)
    {
        ArgumentNullException.ThrowIfNull(block);

        var text = block.Text;
        for (var i = 0; i < text.Length - 2; i++)
        {
            if (text[i] != '.' || text[i + 1] != ' ' || !char.IsUpper(text[i + 2]))
            {
                continue;
            }

            var word = text[..i].Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
            if (word.Count(char.IsLetter) < 2)
            {
                continue;
            }

            return block with { Text = text[..(i + 1)], HelpText = text[(i + 2)..].Trim() };
        }

        return block;
    }
}
