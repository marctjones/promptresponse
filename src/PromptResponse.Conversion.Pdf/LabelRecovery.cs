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
        var qualified = new Dictionary<int, string>();
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

        // Fifth pass: a caption above a group of fields labels every member of
        // it -- rows of a repeating block, and boxes of one entry split across a
        // line. W-9's social security number is three boxes separated by printed
        // dashes under a single caption; without this the first box takes it and
        // the other two are left blank.
        // Deliberately ignores whether another field already claimed the run --
        // one run labels one field on an ordinary form, but a caption over a
        // repeating group belongs to all of its rows, and enforcing exclusivity
        // is exactly what leaves rows two and three of I-9's Supplement B blank.
        var byId = fields.Select((f, i) => (f, i)).ToDictionary(x => x.f.Id, x => x.i);
        foreach (var column in RepeatingRows.Detect(fields).Concat(RepeatingRows.DetectRows(fields)))
        {
            var rows = column.FieldIds
                .Select(id => byId.TryGetValue(id, out var i) ? i : -1)
                .Where(i => i >= 0)
                .ToList();
            if (rows.Count == 0)
            {
                continue;
            }

            var header = labels.TryGetValue(rows[0], out var already)
                ? already
                : Nearest(fields[rows[0]], rows[0], eligible, rejected);
            if (header is null)
            {
                continue;
            }

            foreach (var row in rows)
            {
                if (!labels.ContainsKey(row) && !fields[row].HasLabel)
                {
                    labels[row] = header with { FieldIndex = row };
                }
            }
        }

        // A caption printed between two fields on one line belongs to both.
        // SS-4 writes "[x] Sole proprietor (SSN) ______", where the tick box
        // claims the caption from its right and the entry line beside it is
        // then left with nothing. Sharing is allowed only when the two fields
        // sit on OPPOSITE sides of the run, which is what distinguishes this
        // from two unrelated fields both reaching for the same text.
        var claimant = labels
            .GroupBy(kv => kv.Value.SpanIndex)
            .ToDictionary(g => g.Key, g => g.First().Key);

        foreach (var (field, index) in wanting)
        {
            if (labels.ContainsKey(index) || field.TargetRect is not { } rect)
            {
                continue;
            }

            foreach (var pair in claimant)
            {
                var spanIndex = pair.Key;
                var ownerIndex = pair.Value;
                var span = spans[spanIndex].Rect;
                if (spans[spanIndex].PageNumber != field.PageNumber
                    || fields[ownerIndex].TargetRect is not { } owner)
                {
                    continue;
                }

                // Same line as the run, and on the far side of it from the
                // field that already claimed it.
                var shares = span.Top > rect.Bottom + EdgeTolerance && span.Bottom < rect.Top - EdgeTolerance;
                var opposite = (owner.Right <= span.Left + EdgeTolerance && rect.Left >= span.Right - EdgeTolerance)
                    || (owner.Left >= span.Right - EdgeTolerance && rect.Right <= span.Left + EdgeTolerance);
                var gap = rect.Left >= span.Right ? rect.Left - span.Right : span.Left - rect.Right;

                if (shares && opposite && gap >= -EdgeTolerance && gap <= MaximumHorizontalGap)
                {
                    labels[index] = labels[ownerIndex] with { FieldIndex = index };
                    break;
                }
            }
        }

        // A tick box whose caption is only "Yes" or "No" has a label that says
        // nothing on its own: SS-4 asks "Is this application for a limited
        // liability company (LLC)?" once and then offers two boxes. The answer
        // word is correct and useless, so the question printed at the head of
        // the same line is prefixed to it.
        foreach (var (field, index) in wanting)
        {
            if (!labels.TryGetValue(index, out var found)
                || !IsBareAnswer(spans[found.SpanIndex].Text)
                || field.TargetRect is not { } box)
            {
                continue;
            }

            // Searched over every classified run, not just the label
            // candidates. The question a Yes/No pair answers is usually long
            // enough to have been ruled out as an instruction -- SS-4's "Is
            // this application for a limited liability company (LLC) (or a
            // foreign equivalent)?" is exactly that -- and it is still the
            // question. Nothing is consumed here, so this cannot starve
            // another field.
            var question = spans
                .Where(s => s.PageNumber == field.PageNumber
                    && s.Role != SpanRole.Furniture
                    && !IsBareAnswer(s.Text)
                    && IsUsableLabel(s.Text)
                    && s.Rect.Right <= box.Left + EdgeTolerance
                    && s.Rect.Top > box.Bottom + EdgeTolerance
                    && s.Rect.Bottom < box.Top - EdgeTolerance)
                .OrderBy(s => box.Left - s.Rect.Right)
                .Select(s => s.Text.Trim())
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(question))
            {
                qualified[index] = $"{question} {spans[found.SpanIndex].Text.Trim()}";
            }
        }

        var updatedFields = fields
            .Select((field, i) => labels.TryGetValue(i, out var found)
                ? field with
                {
                    Label = qualified.TryGetValue(i, out var full) ? full : spans[found.SpanIndex].Text.Trim(),
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
    /// <summary>
    /// Whether a run is nothing but answer words, and so carries no question.
    /// </summary>
    /// <remarks>
    /// Tests every token rather than the whole string, because the answer words
    /// of one line often arrive as a single run: SS-4's line 8c extracts as
    /// "Yes No .", which a whole-string test treats as a question and prefixes,
    /// producing the label "Yes No . Yes".
    /// </remarks>
    private static bool IsBareAnswer(string text)
    {
        var words = text.Split([' ', '.', ',', ':', ';'], StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && words.All(w =>
            w.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || w.Equals("no", StringComparison.OrdinalIgnoreCase));
    }

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
        // Preference beats proximity, and by a wide margin. Re-measured across
        // all five label-graded forms, counting the fields whose recovered
        // label agrees with the oracle:
        //
        //   this order                                   211
        //   rank every direction by distance             199
        //   primary side, then nearer of above / below   182
        //
        // The nearest run is very often a DIFFERENT field's caption, and the
        // denser the form the worse that gets: ranking purely by distance costs
        // fed-w9 nine points of recall and fed-ss4 ten. Which side a caption
        // sits on is real information about the layout, and distance alone
        // discards it.
        //
        // Searching BELOW the field was measured as part of that third row, and
        // it is the worst of the three. Form 8822 does print "Title" under the
        // rule you write the title on, and that one field stays wrong: below a
        // field is usually the next field's caption, so admitting the direction
        // to fix one field misreads twenty-nine others.
        var order = DirectionsFor(field, rect);

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

    /// <summary>The sides a field looks for its label on, in preference order.</summary>
    /// <remarks>
    /// <b>Ordered by how often a direction is right when it fires, not by how
    /// often it fires.</b> Measured across all five graded forms:
    /// <code>
    ///   Right    66/68   = 97%
    ///   Left     23/27   = 85%
    ///   Above   122/147  = 83%
    /// </code>
    /// <para>
    /// Above is the most common and the least reliable, which is why putting it
    /// first is worse even though it wins most often: text-first scores 197
    /// against this order's 211. A direction that fires rarely and is nearly
    /// always right belongs ahead of one that fires constantly and is sometimes
    /// wrong, because the rare one only takes fields it has good reason to.
    /// </para>
    /// </remarks>
    public static LabelDirection[] DirectionsFor(DiscoveredField field, PdfRectangle rect) =>
        IsCheckbox(field, rect)
            ? [LabelDirection.Right, LabelDirection.Above]
            : [LabelDirection.Left, LabelDirection.Above];

    /// <summary>Whether a field is a tick box rather than a write-on blank.</summary>
    public static bool IsCheckbox(DiscoveredField field, PdfRectangle rect) =>
        field.ExpectedDataType == "boolean"
        || (rect.Right - rect.Left <= CheckboxMaximumSide && rect.Top - rect.Bottom <= CheckboxMaximumSide);

    /// <summary>
    /// How far a run sits from a field in a given direction, or null when it is
    /// not in that direction at all.
    /// </summary>
    /// <summary>
    /// How far a run sits from a field in a given direction, or null when it is
    /// not in that direction at all. Public so a diagnostic can ask whether a
    /// run was ever reachable, rather than re-deriving these rules and drifting.
    /// </summary>
    public static double? Distance(PdfRectangle field, PdfRectangle span, LabelDirection direction)
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
                // Above means above *this* field, so the run has to line up with
                // it horizontally -- otherwise every field on a row would claim
                // the leftmost caption on the row above.
                //
                // Lining up is not strict overlap. Measured on I-9, six captions
                // sit 2pt above their field and 4 to 66pt to one side, and a
                // strict test rejects every one of them: "Signature of Employer
                // or Authorized Representative" is offset 7pt from the field it
                // labels. The allowance is scaled to the field's own width, so a
                // wide field tolerates a wider offset and a narrow one does not.
                // Strict overlap, and it is load-bearing. Relaxing it so a
                // caption may sit to one side was measured: an allowance of a
                // quarter of the field's width drops I-9 recall from 71% to
                // 59%, half a width to 52%, a full width to 48%. Overlap is
                // what keeps a field to its own column; widen it and fields
                // take each other's captions. A last-resort pass applying the
                // same slack only to fields that found nothing was also tried,
                // and changed no score at all, because the runs it would reach
                // are already claimed.
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
