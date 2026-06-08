namespace PromptResponse.Rendering.Pdf;

/// <summary>
/// What to do with an imported template, based on its assessed quality.
/// </summary>
public enum ImportRecommendation
{
    /// <summary>Labels are human-readable; use the import as-is (minor review aside).</summary>
    UseDirectly,

    /// <summary>Usable, but some labels/types need a human pass before sharing.</summary>
    ReviewRecommended,

    /// <summary>Labels are mostly raw field names — the document-to-apr skill will do far better.</summary>
    UseSkillInstead,
}

/// <summary>Why a particular imported prompt was flagged for review.</summary>
public enum FieldFlagKind
{
    /// <summary>The label looks like a raw AcroForm field name (e.g. <c>f1_1[0]</c>), not a question.</summary>
    CrypticLabel,

    /// <summary>The label is identical to one or more other prompts'.</summary>
    DuplicateLabel,

    /// <summary>A checkbox/button that carries options — likely a radio group that should be a dropdown.</summary>
    AmbiguousChoice,
}

/// <summary>One imported prompt flagged for human review, with the reason.</summary>
/// <param name="PromptId">The imported prompt's id (the PDF field name).</param>
/// <param name="Label">The label as imported.</param>
/// <param name="Kind">Why it was flagged.</param>
/// <param name="Message">A short, human-readable explanation.</param>
public sealed record FieldFlag(string PromptId, string Label, FieldFlagKind Kind, string Message);

/// <summary>
/// A no-AI, heuristic assessment of how good a PDF import turned out — so the tool
/// can tell the user up front whether to use it directly or reach for the
/// <c>document-to-apr</c> skill instead. The dominant signal is whether the PDF's
/// fields carried tooltips (<c>/TU</c>): with them, labels are real questions;
/// without, they degrade to raw field names.
/// </summary>
/// <param name="Score">0–100 overall quality.</param>
/// <param name="Grade">Letter grade (A–F) for the score.</param>
/// <param name="Recommendation">What to do with the import.</param>
/// <param name="Summary">A one-line verdict suitable for CLI/dialog.</param>
/// <param name="FieldCount">Total imported prompts assessed.</param>
/// <param name="TooltipCoverage">Fraction of fields whose label came from a PDF tooltip (0–1).</param>
/// <param name="CrypticLabelRatio">Fraction of labels that look like raw field names (0–1).</param>
/// <param name="DuplicateLabelRatio">Fraction of prompts whose label is shared with another (0–1).</param>
/// <param name="Flags">Per-field review flags.</param>
public sealed record ImportQuality(
    int Score,
    string Grade,
    ImportRecommendation Recommendation,
    string Summary,
    int FieldCount,
    double TooltipCoverage,
    double CrypticLabelRatio,
    double DuplicateLabelRatio,
    IReadOnlyList<FieldFlag> Flags);
