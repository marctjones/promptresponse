using System.Text.Json;
using System.Text.Json.Serialization;
using PromptResponse.Core.Review;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Cli.Commands;

/// <summary>
/// Reports how confidently a submitted form can be processed automatically.
/// </summary>
/// <remarks>
/// <para>
/// This is the receiving end's command. The rest of the CLI serves whoever is authoring
/// or filling a form; this serves whoever is on the other side of the submission, holding
/// a file somebody sent them and deciding what to do with it.
/// </para>
/// <para>
/// They cannot use `validate` for that. The format refuses, absolutely, to reject
/// anything a person writes, so every submission that parses is valid and validity tells
/// a receiver nothing about whether their pipeline can read it. `review` answers the
/// question they actually have: will a machine reading these fields get what the author
/// intended?
/// </para>
/// <para>
/// Exit codes are the point of the command, because a shell script is usually what reads
/// them:
/// </para>
/// <list type="bullet">
///   <item><description><b>0</b> — safe to process automatically.</description></item>
///   <item><description><b>2</b> — at least one field a machine cannot read as intended;
///     route to a person, to a model, or back to the submitter.</description></item>
///   <item><description><b>1</b> — the file could not be read at all.</description></item>
/// </list>
/// <para>
/// Advisories alone exit 0: an answer outside the suggested options is explicitly allowed
/// by the format and is often exactly right. A receiver who wants to see those too can
/// pass <c>--strict</c>, which exits 2 for any finding. The command recommends; the
/// receiver decides.
/// </para>
/// </remarks>
public class ReviewCommand
{
    private readonly IAprSerializer _serializer;

    public ReviewCommand(IAprSerializer serializer) => _serializer = serializer;

    public async Task<int> ExecuteAsync(string[] args)
    {
        var path = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (path is null)
        {
            Console.Error.WriteLine(
                "Usage: apr review <file> [--template=<file>] [--json] [--strict]");
            return 1;
        }

        var templatePath = args.FirstOrDefault(a => a.StartsWith("--template=", StringComparison.Ordinal))
            ?["--template=".Length..];
        if (templatePath is not null && !File.Exists(templatePath))
        {
            Console.Error.WriteLine($"Template not found: {templatePath}");
            return 1;
        }

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"File not found: {path}");
            return 1;
        }

        DocumentReview review;
        FormComparison? comparison = null;
        try
        {
            var submission = _serializer.Deserialize(await File.ReadAllTextAsync(path));
            review = FormReviewer.Review(submission);

            if (templatePath is not null)
            {
                var template = _serializer.Deserialize(await File.ReadAllTextAsync(templatePath));
                comparison = FormComparer.Compare(template, submission);
            }
        }
        catch (SerializationException ex)
        {
            Console.Error.WriteLine($"Could not read {path}: {ex.Message}");
            return 1;
        }

        var strict = args.Contains("--strict");
        if (args.Contains("--json") || args.Contains("-j"))
        {
            WriteJson(path, review, comparison, strict);
        }
        else
        {
            WriteComparison(comparison);
            WriteReport(review);
        }

        // A form whose questions were edited is not a borderline call: the responses
        // answer something other than what was published, so it needs a person whatever
        // the responses themselves look like.
        var formChanged = comparison is { DefinitionIdentical: false };
        var blocked = strict
            ? review.Findings.Count > 0 || formChanged
            : review.Verdict == ReviewVerdict.ReviewRequired || formChanged;
        return blocked ? 2 : 0;
    }

    private static void WriteComparison(FormComparison? comparison)
    {
        if (comparison is null)
        {
            return;
        }

        Console.WriteLine("Form comparison");
        Console.WriteLine("═══════════════");
        Console.WriteLine();

        if (comparison.DefinitionIdentical)
        {
            Console.WriteLine("  The submission answers exactly the questions the template asks.");
            Console.WriteLine("  (Compared as canonical form definition — the same bytes a publisher");
            Console.WriteLine("   signature binds, so responses do not affect the comparison.)");
            Console.WriteLine();
            return;
        }

        Console.WriteLine("  ⚠ The submitted form is NOT the template's form.");
        Console.WriteLine();
        foreach (var f in comparison.Findings)
        {
            Console.WriteLine($"    [{f.Code}] {f.PromptLabel}  ({f.SectionPath})");
            Console.WriteLine($"        {f.Message}");
            if (!string.IsNullOrEmpty(f.Response))
            {
                Console.WriteLine($"        answered: \"{Truncate(f.Response)}\"");
            }
            Console.WriteLine();
        }
    }

    private static void WriteJson(string path, DocumentReview review, FormComparison? comparison, bool strict)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            file = path,
            verdict = review.Verdict,
            // Restated in every report so no downstream system mistakes "reviewRequired"
            // for "invalid". The content of a response can never make a document invalid.
            contentIsAlwaysValid = review.ContentIsAlwaysValid,
            strict,
            promptsConsidered = review.PromptsConsidered,
            needsReview = review.NeedsReviewCount,
            advisory = review.AdvisoryCount,
            blank = review.BlankCount,
            findings = review.Findings,
            formComparison = comparison is null ? null : new
            {
                definitionIdentical = comparison.DefinitionIdentical,
                identityMatches = comparison.IdentityMatches,
                findings = comparison.Findings,
            },
        }, options));
    }

    private static void WriteReport(DocumentReview review)
    {
        Console.WriteLine("Processability review");
        Console.WriteLine("═════════════════════");
        Console.WriteLine();
        Console.WriteLine($"  Verdict:   {Describe(review.Verdict)}");
        Console.WriteLine($"  Considered: {review.PromptsConsidered} field(s) the form actually asks for");
        Console.WriteLine($"  Flagged:   {review.NeedsReviewCount} needing review, {review.AdvisoryCount} advisory");
        Console.WriteLine();

        if (review.Findings.Count == 0)
        {
            Console.WriteLine("  Every answered field matches what the form asked for.");
            return;
        }

        foreach (var group in review.Findings.GroupBy(f => f.Severity).OrderBy(g => g.Key == ReviewSeverity.Advisory))
        {
            Console.WriteLine(group.Key == ReviewSeverity.NeedsReview
                ? "  Needs review — a machine will not read these as intended:"
                : "  Advisory — unusual, but the format allows it and it may be correct:");
            Console.WriteLine();
            foreach (var f in group)
            {
                Console.WriteLine($"    [{f.Code}] {f.PromptLabel}  ({f.SectionPath})");
                Console.WriteLine($"        answered: \"{Truncate(f.Response)}\"");
                Console.WriteLine($"        {f.Message}");
                Console.WriteLine();
            }
        }

        Console.WriteLine("  The document is valid. Any text is a valid response (spec 3.3);");
        Console.WriteLine("  this report is about automatic processing, not about correctness.");
    }

    private static string Describe(ReviewVerdict verdict) => verdict switch
    {
        ReviewVerdict.Processable => "processable — safe to handle automatically",
        ReviewVerdict.ReviewRecommended => "review recommended — advisories only",
        ReviewVerdict.ReviewRequired => "review required — a person or model should look",
        _ => verdict.ToString(),
    };

    private static string Truncate(string value) =>
        value.Length <= 60 ? value : value[..57] + "...";
}
