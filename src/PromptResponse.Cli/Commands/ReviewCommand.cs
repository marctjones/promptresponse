using System.Text.Json;
using System.Text.Json.Serialization;
using PromptResponse.Core.Review;
using PromptResponse.Core.Serialization;
using PromptResponse.Cli.Commands.Reporting;

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
        Core.Models.AprDocument? submissionForNotice = null;
        try
        {
            var submission = _serializer.Deserialize(await File.ReadAllTextAsync(path));
            submissionForNotice = submission;
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

        // A broken signature is a routing signal here, unlike in `validate`. The
        // question this command answers is not "is this document valid" - it is - but
        // "can a machine handle this submission unattended". Somebody attested to this
        // form and it no longer matches: that needs a person, whatever the answers look
        // like.
        var signaturesBroken = submissionForNotice is not null && SignatureNotice.Write(submissionForNotice);

        var strict = args.Contains("--strict");
        if (args.Contains("--json") || args.Contains("-j"))
        {
            ReviewReportWriter.WriteJson(path, review, comparison, strict);
        }
        else
        {
            ReviewReportWriter.WriteComparison(comparison);
            ReviewReportWriter.WriteReport(review);
        }

        // A form whose questions were edited is not a borderline call: the responses
        // answer something other than what was published, so it needs a person whatever
        // the responses themselves look like.
        var formChanged = comparison is { DefinitionIdentical: false };
        var blocked = strict
            ? review.Findings.Count > 0 || formChanged || signaturesBroken
            : review.Verdict == ReviewVerdict.ReviewRequired || formChanged || signaturesBroken;
        return blocked ? 2 : 0;
    }

}
