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
            MeasuredSpan? open = null;

            foreach (var run in page.OrderByDescending(r => r.Rect.Top).ThenBy(r => r.Rect.Left))
            {
                if (open is not null && Continues(open, run, onPage))
                {
                    open = Join(open, run);
                    continue;
                }

                if (open is not null)
                {
                    blocks.Add(open);
                }

                open = run;
            }

            if (open is not null)
            {
                blocks.Add(open);
            }
        }

        return [.. blocks.Select(Split)];
    }

    /// <summary>Whether a run is the continuation of the block above it.</summary>
    private static bool Continues(MeasuredSpan block, MeasuredSpan next, List<PdfRectangle> fields)
    {
        var gap = block.Rect.Bottom - next.Rect.Top;
        if (gap < -IndentTolerance || gap > block.Height * LineGapMultiple)
        {
            return false;
        }

        // Flush with the block or indented under it, and starting within it.
        if (next.Rect.Left < block.Rect.Left - IndentTolerance || next.Rect.Left >= block.Rect.Right)
        {
            return false;
        }

        // Same size of type. A caption and the heading above it can be adjacent
        // and aligned without being one block.
        if (Math.Max(block.Height, next.Height) > Math.Min(block.Height, next.Height) * 1.5)
        {
            return false;
        }

        // A field between the two lines ends the block.
        return !fields.Any(f =>
            f.Top <= block.Rect.Bottom + IndentTolerance
            && f.Bottom >= next.Rect.Top - IndentTolerance
            && f.Right > next.Rect.Left && f.Left < block.Rect.Right);
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
