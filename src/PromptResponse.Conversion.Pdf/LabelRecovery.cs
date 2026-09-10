using Excise.Core.Document;

namespace PromptResponse.Conversion.Pdf;

/// <summary>Which side of a field its label was found on.</summary>
public enum LabelDirection
{
    /// <summary>Printed above the field, the usual layout for a boxed form.</summary>
    Above,

    /// <summary>Printed to the left, the usual layout for a ruled line.</summary>
    Left,

    /// <summary>Printed to the right, which is where a tick box's name always sits.</summary>
    Right,
}

/// <summary>What label recovery did, in terms a caller can check.</summary>
/// <param name="Fields">The fields, with recovered labels attached.</param>
/// <param name="Spans">
/// The spans, with claimed runs promoted to <see cref="SpanRole.Label"/> and
/// over-claimed ones demoted to <see cref="SpanRole.Instruction"/>.
/// </param>
/// <param name="Recovered">Fields that gained a label they did not have.</param>
/// <param name="GroupInstructions">
/// Runs rejected because too many fields wanted them — see
/// <see cref="LabelRecovery.GroupInstructionClaims"/>.
/// </param>

public sealed record LabelRecoveryResult(
    IReadOnlyList<DiscoveredField> Fields,
    IReadOnlyList<TextSpan> Spans,
    int Recovered,
    int GroupInstructions);

/// <summary>
/// Attaches the question printed on the page to the field it belongs to.
/// </summary>
/// <remarks>
/// <para>
/// Two thirds of the corpus's 446 importable fields arrive named <c>f1_01[0]</c>
/// with no <c>/TU</c> tooltip, so for most of a real form the question exists
/// only as ink near the blank. This phase is what turns that ink into a label.
/// </para>
/// <para>
/// <b>Proximity is the primary signal, not text shape.</b> That is a correction
/// established by measurement: classifying runs by how they read produced 442
/// labels on a 23-field form while filing the real questions as instructions.
/// Being next to a field is what makes a run a question, so
/// <see cref="SpanClassifier"/> now only rules text out and this phase is the
/// only thing that may rule it in.
/// </para>
/// <para>
/// <b>Direction depends on the kind of field.</b> A tick box takes its name from
/// the text on its right; a write-on blank takes its label from the left, or
/// from above in a boxed form. Using one "nearest text" rule for both is how
/// W-9's seven tax-classification boxes all end up labelled "Check the
/// appropriate box" — the group instruction above them — instead of
/// "Individual/sole proprietor", "C corporation", and so on.
/// </para>
/// </remarks>
public static class LabelRecovery
{
    /// <summary>
    /// A widget no larger than this on both sides is a tick box.
    /// </summary>
    /// <remarks>
    /// Measured across the corpus: check-box widgets run 6-12pt square, while
    /// the smallest text blank is over 40pt wide. The declared data type is used
    /// first where it exists; this is the fallback for a field discovered from
    /// the page rather than from an AcroForm, which has no declared type.
    /// </remarks>
    public const double CheckboxMaximumSide = 20.0;

    /// <summary>How far a run may sit horizontally from a field and still be its label, in points.</summary>
    /// <remarks>
    /// Wide enough to cross the gap from a ruled line's caption, narrow enough
    /// that a field never reaches into the next column. W-9's text column is
    /// roughly 240pt, so a full column width would let a field claim its
    /// neighbour's label.
    /// </remarks>
    public const double MaximumHorizontalGap = 96.0;

    /// <summary>How far above a field a run may sit and still be its label, in points.</summary>
    /// <remarks>
    /// About two lines at the corpus's 7-8pt body size. Beyond that the run
    /// belongs to whatever sits between it and the field.
    /// </remarks>
    public const double MaximumVerticalGap = 22.0;

    /// <summary>Slack when deciding which side of a field a run sits on, in points.</summary>
    public const double EdgeTolerance = 2.0;

    /// <summary>
    /// How many fields must want the same run before it is treated as a group
    /// instruction rather than any one field's label.
    /// </summary>
    /// <remarks>
    /// The deterministic answer to "instructions read as fields". A line that
    /// three separate fields all consider their nearest text is describing the
    /// group, not any member of it: on W-9, "3a Check the appropriate box for
    /// federal tax classification" sits above seven tick boxes. Handing it to
    /// all seven would manufacture seven duplicate questions that each look
    /// individually plausible, which is the expensive kind of wrong.
    /// </remarks>
    public const int GroupInstructionClaims = 3;

    /// <summary>Longest run, in words, that the rescue pass may take as a label.</summary>
    /// <remarks>
    /// The rescue exists because the classifier rules out text that really is a
    /// label — I-9 prints "Check here if you used an alternative procedure
    /// authorized by DHS" beside a tick box, word for word the author's own
    /// label, and it is discarded for starting with "Check". Letting a field
    /// with no other candidate take a ruled-out run recovers those.
    /// <para>
    /// Unbounded, it also lets in the genuine instructions: "I attest, under
    /// penalty of perjury, that I have assisted..." became the label for four
    /// signature fields. Swept against the held-back tooltip oracle on fed-i9:
    /// </para>
    /// <code>
    ///   off          recall 68%   precision 85%   agreed 87
    ///   &lt;= 12 words  recall 68%   precision 85%   agreed 87
    ///   &lt;= 16 words  recall 71%   precision 85%   agreed 91
    ///   unbounded    recall 72%   precision 76%   agreed 92
    /// </code>
    /// <para>
    /// 16 buys four fields for nothing. Unbounded buys one more and costs nine
    /// points of precision, which is the wrong trade: a wrong label is an
    /// unanswerable question, while a missing one is visibly missing.
    /// </para>
    /// </remarks>
    public const int RescueMaximumWords = 16;


    /// <summary>Attaches printed text to the fields that have no label yet.</summary>
    public static LabelRecoveryResult Recover(
        IReadOnlyList<DiscoveredField> fields,
        IReadOnlyList<TextSpan> spans)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(spans);

        // Only runs that survived classification are eligible. A heading, a
        // sentence of guidance, or page furniture is not a field's question
        // however close to it it happens to sit.
        var eligible = spans
            .Select((span, index) => (span, index))
            .Where(s => s.span.Role is SpanRole.Unclassified && IsUsableLabel(s.span.Text))
            .ToList();

        var wanting = fields
            .Select((field, index) => (field, index))
            .Where(f => !f.field.HasLabel && f.field.TargetRect is not null)
            .ToList();

        var rejected = new HashSet<int>();
        List<Proposal> proposals;

        // Rejecting a group instruction can promote a field's second choice into
        // contention, which may itself turn out to be over-claimed. Iterate
        // until the set is stable rather than assuming one pass settles it; the
        // bound is belt-and-braces, since each round only ever removes runs.
        for (var round = 0; ; round++)
        {
            proposals = [.. wanting
                .Select(f => Nearest(f.field, f.index, eligible, rejected))
                .OfType<Proposal>()];

            var overClaimed = proposals
                .GroupBy(p => p.SpanIndex)
                .Where(g => g.Count() >= GroupInstructionClaims)
                .Select(g => g.Key)
                .ToList();

            if (overClaimed.Count == 0 || round >= 8)
            {
                break;
            }

            rejected.UnionWith(overClaimed);
        }

        // Greedy nearest-first assignment. One run labels at most one field, so
        // two fields sharing a caption cannot both claim it -- the closer one
        // wins and the other is left for the review queue.
        var labels = new Dictionary<int, Proposal>();
        var taken = new HashSet<int>();
        foreach (var proposal in proposals.OrderBy(p => p.Distance))
        {
            if (taken.Contains(proposal.SpanIndex) || labels.ContainsKey(proposal.FieldIndex))
            {
                continue;
            }

            taken.Add(proposal.SpanIndex);
            labels[proposal.FieldIndex] = proposal;
        }

        // Second pass: a field that found nothing at all may take a run the
        // classifier ruled out, provided it is as close as a real label would
        // be. I-9 prints "Check here if you used an alternative procedure
        // authorized by DHS" beside a tick box -- word for word the author's
        // own label -- and the classifier discards it for starting with
        // "Check". Restricted to fields with no other candidate, and to the
        // same distances an ordinary label must satisfy, so it can only add
        // answers where there were none.
        var rescuedFrom = spans
            .Select((span, index) => (span, index))
            .Where(s => s.span.Role is SpanRole.Heading or SpanRole.Instruction
                && IsUsableLabel(s.span.Text)
                && s.span.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= RescueMaximumWords)
            .ToList();

        foreach (var (field, index) in wanting)
        {
            if (labels.ContainsKey(index))
            {
                continue;
            }

            var rescue = Nearest(field, index, rescuedFrom, rejected);
            if (rescue is not null && !taken.Contains(rescue.SpanIndex))
            {
                taken.Add(rescue.SpanIndex);
                labels[index] = rescue;
            }
        }

        var updatedFields = fields
            .Select((field, i) => labels.TryGetValue(i, out var found)
                ? field with
                {
                    Label = spans[found.SpanIndex].Text.Trim(),
                    LabelSource = LabelSource.NearbyText,
                    HelpText = spans[found.SpanIndex].HelpText,
                    LabelFoundAt = found.Direction,
                    NeedsReview = null,
                }
                : field)
            .ToList();

        var updatedSpans = spans
            .Select((span, i) => taken.Contains(i) ? span with { Role = SpanRole.Label }
                : rejected.Contains(i) ? span with { Role = SpanRole.Instruction }
                : span)
            .ToList();

        return new LabelRecoveryResult(updatedFields, updatedSpans, labels.Count, rejected.Count);
    }

    /// <summary>
    /// Whether a run could be anybody's question at all.
    /// </summary>
    /// <remarks>
    /// A stray glyph is not a label however close it sits. W-9's address field
    /// was labelled "S" — the tail of a split run — which no distance rule would
    /// ever reject, because it genuinely is the nearest text.
    /// </remarks>
    private static bool IsUsableLabel(string text) =>
        text.Split([' '], StringSplitOptions.RemoveEmptyEntries)
            .Any(w => w.Count(char.IsLetter) >= 2);

    private sealed record Proposal(int FieldIndex, int SpanIndex, LabelDirection Direction, double Distance);

    /// <summary>
    /// The best label candidate for one field, searched in the order that field's
    /// kind implies.
    /// </summary>
    private static Proposal? Nearest(
        DiscoveredField field,
        int fieldIndex,
        List<(TextSpan span, int index)> eligible,
        HashSet<int> rejected)
    {
        var rect = field.TargetRect!.Value;
        var candidates = eligible.Where(e => e.span.PageNumber == field.PageNumber && !rejected.Contains(e.index));

        // A tick box is named on its right; a blank is captioned on its left, or
        // above it in a boxed form. Preference order matters more than the set:
        // a tick box usually has *something* above it too, and that something is
        // usually the instruction for its whole group.
        // Preference beats proximity, measured: ranking every direction by
        // distance instead scores 80 against this order's 81 on fed-i9 and ties
        // on ct-w4. Left and Right are exactly right when they fire (15 of 15
        // on I-9) while Above is right 81% of the time, so trying the reliable
        // sides first is worth more than taking whatever is nearest.
        var order = IsCheckbox(field, rect)
            ? new[] { LabelDirection.Right, LabelDirection.Above }
            : [LabelDirection.Left, LabelDirection.Above];

        foreach (var direction in order)
        {
            var best = candidates
                .Select(c => (c.index, distance: Distance(rect, c.span.Rect, direction)))
                .Where(c => c.distance is not null)
                .OrderBy(c => c.distance!.Value)
                .ToList();

            if (best.Count > 0)
            {
                return new Proposal(fieldIndex, best[0].index, direction, best[0].distance!.Value);
            }
        }

        return null;
    }

    /// <summary>Whether a field is a tick box rather than a write-on blank.</summary>
    private static bool IsCheckbox(DiscoveredField field, PdfRectangle rect) =>
        field.ExpectedDataType == "boolean"
        || (rect.Right - rect.Left <= CheckboxMaximumSide && rect.Top - rect.Bottom <= CheckboxMaximumSide);

    /// <summary>
    /// How far a run sits from a field in a given direction, or null when it is
    /// not in that direction at all.
    /// </summary>
    private static double? Distance(PdfRectangle field, PdfRectangle span, LabelDirection direction)
    {
        switch (direction)
        {
            case LabelDirection.Left:
            case LabelDirection.Right:
            {
                // Beside means beside: the run has to share the field's line,
                // otherwise a caption two rows up would qualify as "to the left".
                if (span.Top <= field.Bottom + EdgeTolerance || span.Bottom >= field.Top - EdgeTolerance)
                {
                    return null;
                }

                var gap = direction is LabelDirection.Left
                    ? field.Left - span.Right
                    : span.Left - field.Right;

                return gap >= -EdgeTolerance && gap <= MaximumHorizontalGap ? Math.Max(0, gap) : null;
            }

            case LabelDirection.Above:
            {
                // Above means above *this* field, so the run has to overlap it
                // horizontally -- otherwise every field on a row would claim the
                // leftmost caption on the row above.
                if (span.Right <= field.Left || span.Left >= field.Right)
                {
                    return null;
                }

                var gap = span.Bottom - field.Top;
                return gap >= -EdgeTolerance && gap <= MaximumVerticalGap ? Math.Max(0, gap) : null;
            }

            default:
                return null;
        }
    }
}
