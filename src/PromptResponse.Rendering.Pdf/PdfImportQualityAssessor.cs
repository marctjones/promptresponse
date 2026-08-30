namespace PromptResponse.Rendering.Pdf;

/// <summary>Applies deterministic, no-AI quality heuristics to a PDF import.</summary>
internal static class PdfImportQualityAssessor
{
    public static ImportQuality Assess(IReadOnlyList<PdfImportFieldMapping> mappings)
    {
        var total = mappings.Count;
        var flags = new List<FieldFlag>();
        var labelCounts = mappings
            .GroupBy(mapping => mapping.Prompt.Label, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var tooltipCount = 0;
        var crypticCount = 0;

        foreach (var mapping in mappings)
        {
            if (mapping.HadTooltip) tooltipCount++;
            var label = mapping.Prompt.Label;
            if (IsCrypticLabel(label))
            {
                crypticCount++;
                flags.Add(new FieldFlag(mapping.Prompt.Id, label, FieldFlagKind.CrypticLabel,
                    "Label looks like a raw PDF field name, not a question."));
            }
            else if (labelCounts[label] > 1)
            {
                flags.Add(new FieldFlag(mapping.Prompt.Id, label, FieldFlagKind.DuplicateLabel,
                    $"Label is shared by {labelCounts[label]} fields."));
            }

            if (mapping.IsButton && mapping.OptionCount > 0)
            {
                flags.Add(new FieldFlag(mapping.Prompt.Id, label, FieldFlagKind.AmbiguousChoice,
                    "Checkbox/button carries options — likely a radio group that should be a dropdown."));
            }
        }

        var tooltipCoverage = Ratio(tooltipCount, total);
        var crypticRatio = Ratio(crypticCount, total);
        var duplicateRatio = Ratio(mappings.Count(mapping => labelCounts[mapping.Prompt.Label] > 1), total);
        var score = Score(crypticRatio, duplicateRatio);
        var grade = score >= 90 ? "A" : score >= 75 ? "B" : score >= 60 ? "C" : score >= 40 ? "D" : "F";
        var recommendation = score >= 70 ? ImportRecommendation.UseDirectly :
            score >= 40 ? ImportRecommendation.ReviewRecommended : ImportRecommendation.UseSkillInstead;
        var readablePercentage = (int)Math.Round((1 - crypticRatio) * 100);
        var summary = Summary(recommendation, score, grade, readablePercentage, total);

        return new ImportQuality(score, grade, recommendation, summary, total,
            tooltipCoverage, crypticRatio, duplicateRatio, flags);
    }

    private static int Score(double crypticRatio, double duplicateRatio)
    {
        var duplicatePenalty = (int)Math.Round(Math.Min(15, duplicateRatio * 30));
        return (int)Math.Clamp(Math.Round((1 - crypticRatio) * 100) - duplicatePenalty, 0, 100);
    }

    private static string Summary(ImportRecommendation recommendation, int score, string grade, int readablePercentage, int total) => recommendation switch
    {
        ImportRecommendation.UseDirectly =>
            $"Good ({score}/100, {grade}). {readablePercentage}% of {total} fields have human-readable labels — use directly.",
        ImportRecommendation.ReviewRecommended =>
            $"Fair ({score}/100, {grade}). {readablePercentage}% of {total} fields have readable labels — review before sharing.",
        _ => $"Poor ({score}/100, {grade}). Only {readablePercentage}% of {total} fields have readable labels " +
             "(the PDF lacks field tooltips) — use the document-to-apr skill, or run it to enrich this import.",
    };

    private static double Ratio(int numerator, int denominator) => denominator == 0 ? 0 : (double)numerator / denominator;

    private static bool IsCrypticLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return true;
        var trimmed = label.Trim();
        return trimmed.Contains('[') || trimmed.StartsWith('#') ||
               (!trimmed.Contains(' ') && trimmed.Length <= 12 && trimmed.Any(char.IsDigit) && trimmed.Any(char.IsLetter));
    }
}
