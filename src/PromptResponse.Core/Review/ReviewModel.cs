namespace PromptResponse.Core.Review;

/// <summary>How much attention a finding deserves.</summary>
/// <remarks>
/// Neither level means the document is invalid. A response can never be invalid
/// (specification 3.3, 6.1); these describe how confidently a machine can process the
/// answer, which is a different question and the receiver's question, not the format's.
/// </remarks>
public enum ReviewSeverity
{
    /// <summary>Unusual but plausible. A person reading it would probably shrug.</summary>
    /// <remarks>
    /// An answer outside the suggested options or the suggested bounds. The author offered
    /// a shortlist and someone said something else, which the format explicitly allows and
    /// which is often exactly right - "other", "see attached", a figure the slider could
    /// not reach.
    /// </remarks>
    Advisory,

    /// <summary>A machine reading this field will get something it did not expect.</summary>
    /// <remarks>
    /// A response that does not parse as its declared type, does not match the pattern the
    /// author supplied, or fails a rule the author wrote themselves. Automatic processing
    /// of this field will either fail or silently do the wrong thing.
    /// </remarks>
    NeedsReview,
}

/// <summary>What a receiving system should do with the document.</summary>
/// <remarks>
/// A recommendation, never a decision. The receiver sets policy - some will process a
/// form with three advisories, others will route anything imperfect to a person. The
/// report supplies the evidence and stays out of the judgement.
/// </remarks>
public enum ReviewVerdict
{
    /// <summary>Nothing flagged. Every answered field matches what the author asked for.</summary>
    Processable,

    /// <summary>Only advisories. Usually processable; worth a glance if the stakes are high.</summary>
    ReviewRecommended,

    /// <summary>At least one field a machine cannot read as intended.</summary>
    ReviewRequired,
}

/// <summary>One field that did not follow its hints.</summary>
/// <param name="PromptId">The prompt's id, for addressing the field programmatically.</param>
/// <param name="PromptLabel">The prompt's label, for showing a person.</param>
/// <param name="SectionPath">Section titles from the document root, so the field is findable.</param>
/// <param name="Code">A stable machine-readable code. Route on this, not on the message.</param>
/// <param name="Severity">How much attention it deserves.</param>
/// <param name="Message">A sentence for a human.</param>
/// <param name="Response">What was actually written, so a reviewer need not open the file.</param>
public sealed record ReviewFinding(
    string PromptId,
    string PromptLabel,
    string SectionPath,
    string Code,
    ReviewSeverity Severity,
    string Message,
    string Response);

/// <summary>What a receiving system needs to know before processing a submission.</summary>
public sealed class DocumentReview
{
    /// <summary>Every field that did not follow its hints, in document order.</summary>
    public IReadOnlyList<ReviewFinding> Findings { get; init; } = [];

    /// <summary>The recommendation. See <see cref="ReviewVerdict"/>.</summary>
    public ReviewVerdict Verdict { get; init; }

    /// <summary>Prompts considered - hidden and not-expected fields are excluded.</summary>
    public int PromptsConsidered { get; init; }

    /// <summary>Prompts left blank that the form expected an answer to.</summary>
    public int BlankCount { get; init; }

    /// <summary>
    /// Always true for anything this report covers.
    /// </summary>
    /// <remarks>
    /// Stated explicitly so nobody reads "review required" as "invalid". Structural
    /// validity is a separate question answered by the validator; the content of a
    /// response can never make a document invalid, however strange it looks.
    /// </remarks>
    public bool ContentIsAlwaysValid => true;

    /// <summary>Findings at <see cref="ReviewSeverity.NeedsReview"/>.</summary>
    public int NeedsReviewCount => Findings.Count(f => f.Severity == ReviewSeverity.NeedsReview);

    /// <summary>Findings at <see cref="ReviewSeverity.Advisory"/>.</summary>
    public int AdvisoryCount => Findings.Count(f => f.Severity == ReviewSeverity.Advisory);
}
