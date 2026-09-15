namespace PromptResponse.Conversion.Pdf;

/// <summary>
/// Decides what each run of printed text is doing on the page.
/// </summary>
/// <remarks>
/// <para>
/// The phase with the largest downside when wrong, and the only one with no
/// mechanical oracle. Reading instructions as questions is how a model produced
/// <b>130 "fields" for a W-4 whose ground truth is 19</b> — the output was not
/// slightly wrong, it was buried. On a six-page W-9 that is mostly instructions,
/// getting this wrong does not degrade the result so much as drown it.
/// </para>
/// <para>
/// So the bias is deliberate and one-directional: <b>when in doubt, call it an
/// instruction.</b> A question wrongly filed as guidance is a missing field,
/// which the coverage oracle catches by name. Guidance wrongly filed as a
/// question is an unanswerable prompt in a real person's form, and nothing
/// downstream catches it. The first failure is visible and cheap; the second is
/// invisible and expensive.
/// </para>
/// <para>
/// <b>This phase rules text out; it never rules text in.</b> Its first version
/// ended in <c>return SpanRole.Label</c> — anything short that was not obviously
/// a heading or a sentence became a question — and on a 23-field W-9 that
/// produced <b>442 labels</b>: "Give form to the", "(Rev. March 2024)" and
/// "Department of the Treasury" were all promoted, while the real questions
/// ("1 Name of entity/individual") were filed as instructions because they read
/// like prose. Text shape simply does not separate the two, so the fallthrough
/// is now <see cref="SpanRole.Unclassified"/> and only
/// <see cref="LabelRecovery"/> — which can see that a field sits beside the
/// text — may promote a run to <see cref="SpanRole.Label"/>. That makes the 442
/// result structurally impossible rather than a threshold to tune.
/// </para>
/// <para>
/// Every signal is drawn from what the corpus actually contains, and none of it
/// requires a model.
/// </para>
/// </remarks>
public static class SpanClassifier
{
    /// <summary>How much taller than body text a run must be to read as a heading.</summary>
    /// <remarks>
    /// Measured on the corpus: W-9's body is 8pt with its title at 14, I-9's is
    /// 7pt with its title at 14, and CT DMV J-23's is 8pt with
    /// "IDENTIFICATION REQUIRED" at 14. Body text itself varies by a point or so
    /// between runs, so the bar sits above that noise and well below a real
    /// heading.
    /// </remarks>
    public const double HeadingSizeRatio = 1.3;

    /// <summary>Words beyond which a run reads as prose rather than a question.</summary>
    /// <remarks>
    /// Form labels are short — "Business name/disregarded entity name", "City,
    /// state, and ZIP code". Once a run runs past about a dozen words it is
    /// almost always a sentence of guidance, and the corpus bears that out.
    /// </remarks>
    public const int MaximumLabelWords = 12;

    /// <summary>Words past which a run ending in a full stop reads as a sentence.</summary>
    /// <remarks>
    /// Swept against the held-back tooltip oracle rather than chosen. At 6 this
    /// ruled out real labels: I-9's "Last Name (Family Name) from Section 1."
    /// is seven words and ends in a full stop, so the question sitting 2pt from
    /// its field was filed as guidance and never reached pairing. Raising it to
    /// 8 takes I-9 label recall from 63% to 68% and precision from 84% to 85%.
    /// <para>
    /// Higher is not better. 10, 12 and "never" all score identically to 8 on
    /// I-9, but 12 and above drop ct-w4's precision from 78% to 70% by letting
    /// real instructions through. 8 is where both forms are best.
    /// </para>
    /// </remarks>
    public const int SentenceWords = 8;

    /// <summary>Classifies one run of text.</summary>
    public static SpanRole Classify(MeasuredSpan span)
    {
        ArgumentNullException.ThrowIfNull(span);

        var text = span.Text.Trim();
        if (text.Length == 0)
        {
            return SpanRole.Furniture;
        }

        if (IsFurniture(text) || IsRotated(span))
        {
            return SpanRole.Furniture;
        }

        // Size wins over shape. A short line of large text is a heading even if
        // it would otherwise look like a label, which is what "Part I" and
        // "General Instructions" are on the real forms.
        if (span.RelativeSize >= HeadingSizeRatio && span.WordCount <= MaximumLabelWords)
        {
            return SpanRole.Heading;
        }

        if (IsSentence(text) || span.WordCount > MaximumLabelWords)
        {
            return SpanRole.Instruction;
        }

        // Deliberately not Label. Nothing about how a run reads makes it a
        // question; see the remarks on this class.
        return SpanRole.Unclassified;
    }

    /// <summary>
    /// Whether a run is rotated text read as though it were horizontal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// W-9 prints "Print or type. See Specific Instructions on page 3" rotated
    /// down its left margin. Word extraction reports those glyphs in reading
    /// order regardless, so the strip arrives as fragments — "rint
    /// Instructions", "pecific", "type.", "page" — that are short, look nothing
    /// like sentences, and sit immediately to the <em>left</em> of real fields.
    /// That is the worst possible combination: they would win a nearest-text
    /// contest against the actual label.
    /// </para>
    /// <para>
    /// The tell is the bounding box. A horizontal run is always wider than it is
    /// tall; these are the reverse — measured on W-9 page 1, the fragments are
    /// 18×46, 8×25, 8×19 and 8×18 points, while every genuine run on the page is
    /// wider than tall. Aspect ratio is used rather than absolute height because
    /// height alone cannot tell rotated text from a large heading: "Form W-9" is
    /// 24pt tall against a 7pt body, which a height threshold would discard
    /// along with the sidebar.
    /// </para>
    /// </remarks>
    private static bool IsRotated(MeasuredSpan span)
    {
        var width = span.Rect.Right - span.Rect.Left;
        var height = span.Rect.Top - span.Rect.Bottom;

        // A single glyph is legitimately taller than wide, so the rule needs at
        // least two characters before an aspect ratio means anything.
        return span.Text.Trim().Length >= 2 && height > width;
    }

    /// <summary>Classifies every run, leaving each one's geometry untouched.</summary>
    public static IReadOnlyList<TextSpan> Classify(IEnumerable<MeasuredSpan> spans) =>
        [.. spans.Select(s => new TextSpan(s.PageNumber, s.Text, s.Rect, Classify(s), s.HelpText))];

    /// <summary>
    /// Pre-printed page furniture: numbering, form codes, rules, stray marks.
    /// </summary>
    private static bool IsFurniture(string text)
    {
        // A run with no letters at all is a rule, a row of dots, or a page
        // number. None of them is a question and none is guidance.
        if (!text.Any(char.IsLetter))
        {
            return true;
        }

        // "Page 3", "Page 3 of 6", "Cat. No. 10231X", "Form W-9 (Rev. 3-2024)".
        return text.StartsWith("Page ", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Cat. No", StringComparison.OrdinalIgnoreCase)
            || (text.StartsWith("Form ", StringComparison.OrdinalIgnoreCase) && text.Contains("Rev.", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Whether a run reads as a sentence of guidance rather than a question.
    /// </summary>
    /// <remarks>
    /// Terminal punctuation is the strongest single signal, but a label may end
    /// in a full stop too ("1 Name of entity/individual."), so it is not used
    /// alone: a run must also be reasonably long, or begin the way instructions
    /// on these forms begin.
    /// </remarks>
    private static bool IsSentence(string text)
    {
        var startsAsGuidance =
            text.StartsWith("See ", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Note:", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Enter ", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Check ", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("If you", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Do not", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("For ", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Under penalties", StringComparison.OrdinalIgnoreCase);

        if (startsAsGuidance)
        {
            return true;
        }

        var endsSentence = text.EndsWith('.') || text.EndsWith(':') is false && text.EndsWith('!');
        return endsSentence && text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > SentenceWords;
    }
}
