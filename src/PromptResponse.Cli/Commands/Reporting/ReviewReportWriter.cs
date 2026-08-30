using System.Text.Json;
using System.Text.Json.Serialization;
using PromptResponse.Core.Review;

namespace PromptResponse.Cli.Commands.Reporting;

/// <summary>Writes the human and machine-readable reports for a completed review.</summary>
internal static class ReviewReportWriter
{
    internal static void WriteComparison(FormComparison? comparison)
    {
        if (comparison is null) return;
        Console.WriteLine("Form comparison\n═══════════════\n");
        if (comparison.DefinitionIdentical) { Console.WriteLine("  The submission answers exactly the questions the template asks."); Console.WriteLine("  (Compared as canonical form definition — the same bytes a publisher"); Console.WriteLine("   signature binds, so responses do not affect the comparison.)\n"); return; }
        Console.WriteLine("  ⚠ The submitted form is NOT the template's form.\n");
        foreach (var finding in comparison.Findings) { Console.WriteLine($"    [{finding.Code}] {finding.PromptLabel}  ({finding.SectionPath})"); Console.WriteLine($"        {finding.Message}"); if (!string.IsNullOrEmpty(finding.Response)) Console.WriteLine($"        answered: \"{Truncate(finding.Response)}\""); Console.WriteLine(); }
    }

    internal static void WriteJson(string path, DocumentReview review, FormComparison? comparison, bool strict)
    {
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
        Console.WriteLine(JsonSerializer.Serialize(new { file = path, verdict = review.Verdict, contentIsAlwaysValid = review.ContentIsAlwaysValid, strict, promptsConsidered = review.PromptsConsidered, needsReview = review.NeedsReviewCount, advisory = review.AdvisoryCount, blank = review.BlankCount, findings = review.Findings, formComparison = comparison is null ? null : new { definitionIdentical = comparison.DefinitionIdentical, identityMatches = comparison.IdentityMatches, findings = comparison.Findings } }, options));
    }

    internal static void WriteReport(DocumentReview review)
    {
        Console.WriteLine("Processability review\n═════════════════════\n");
        Console.WriteLine($"  Verdict:   {Describe(review.Verdict)}");
        Console.WriteLine($"  Considered: {review.PromptsConsidered} field(s) the form actually asks for");
        Console.WriteLine($"  Flagged:   {review.NeedsReviewCount} needing review, {review.AdvisoryCount} advisory\n");
        if (review.Findings.Count == 0) { Console.WriteLine("  Every answered field matches what the form asked for."); return; }
        foreach (var group in review.Findings.GroupBy(finding => finding.Severity).OrderBy(group => group.Key == ReviewSeverity.Advisory)) { Console.WriteLine(group.Key == ReviewSeverity.NeedsReview ? "  Needs review — a machine will not read these as intended:" : "  Advisory — unusual, but the format allows it and it may be correct:"); Console.WriteLine(); foreach (var finding in group) { Console.WriteLine($"    [{finding.Code}] {finding.PromptLabel}  ({finding.SectionPath})"); Console.WriteLine($"        answered: \"{Truncate(finding.Response)}\""); Console.WriteLine($"        {finding.Message}\n"); } }
        Console.WriteLine("  The document is valid. Any text is a valid response (spec 3.3);"); Console.WriteLine("  this report is about automatic processing, not about correctness.");
    }

    private static string Describe(ReviewVerdict verdict) => verdict switch { ReviewVerdict.Processable => "processable — safe to handle automatically", ReviewVerdict.ReviewRecommended => "review recommended — advisories only", ReviewVerdict.ReviewRequired => "review required — a person or model should look", _ => verdict.ToString() };
    private static string Truncate(string value) => value.Length <= 60 ? value : value[..57] + "...";
}
