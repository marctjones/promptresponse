using System.Globalization;
using PromptResponse.Core.Expressions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Validation;

namespace PromptResponse.Core.Review;

/// <summary>
/// Reports how confidently a submitted form can be processed automatically.
/// </summary>
/// <remarks>
/// <para>
/// The format refuses, deliberately and absolutely, to reject anything a person writes
/// (specification 3.3, 6.1). That protects the filler, and it hands the receiver a
/// problem: every submission is valid, so validity tells them nothing about whether
/// their pipeline can read it.
/// </para>
/// <para>
/// This answers the receiver's question instead, which is a different one - not "is this
/// document valid" but "will a machine reading this field get what the author intended".
/// Specification 6.2 already defines the vocabulary for it: warnings that say "this may
/// not be what you meant" without ever saying "you may not write this".
/// </para>
/// <para>
/// It recommends and never decides. A receiver processing expense claims may accept
/// three advisories without blinking; one processing security clearances may route
/// anything imperfect to a person. Both are right, and neither is the format's business,
/// so the report carries stable codes to route on and stays out of the judgement.
/// </para>
/// <para>
/// Fields the form itself does not expect - hidden by <c>exprHidden</c>, or not expected
/// by <c>exprExpected</c> - are skipped entirely. A conditional branch that does not
/// apply is not a gap, and flagging it would bury the real findings under noise from
/// every question the filler was right to skip.
/// </para>
/// </remarks>
public static class FormReviewer
{
    /// <summary>Reviews a filled form for automatic processability.</summary>
    public static DocumentReview Review(AprDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var context = FormExpressions.BuildContext(document);
        var typeInspector = new DataTypeValidator();
        var findings = new List<ReviewFinding>();
        var considered = 0;
        var blanks = 0;

        void Walk(Section section, string path)
        {
            var here = string.IsNullOrWhiteSpace(path) ? section.Title : $"{path} / {section.Title}";

            foreach (var prompt in section.Prompts)
            {
                // A hidden field is one the form itself says does not apply. Skipping it
                // is what makes the rest of the report worth reading - otherwise every
                // question the filler was right to skip buries the real findings.
                if (FormExpressions.IsHidden(prompt, context))
                {
                    continue;
                }

                // exprExpected is an opt-in marker meaning "this one is expected", not a
                // statement that anything without it is unwanted. An earlier draft treated
                // its absence as "not asked for" and skipped every ordinary prompt in the
                // document, reporting a clean bill of health for a form it had not looked
                // at. Only an exprExpected that is present and evaluates false says the
                // form is not expecting an answer right now.
                var expectedRule = prompt.Hints?.ExprExpected;
                var notExpectedNow = !string.IsNullOrWhiteSpace(expectedRule)
                                     && !FormExpressions.IsExpected(prompt, context);

                considered++;
                var response = prompt.Response ?? string.Empty;

                void Flag(string code, ReviewSeverity severity, string message) =>
                    findings.Add(new ReviewFinding(
                        prompt.Id, prompt.Label, here, code, severity, message, response));

                if (string.IsNullOrWhiteSpace(response))
                {
                    if (!notExpectedNow)
                    {
                        blanks++;
                        Flag("BLANK", ReviewSeverity.Advisory,
                            "No answer given. Whether that matters is the receiving " +
                            "workflow's call: the format has no required responses.");
                    }
                    continue;   // Nothing further to say about an empty answer.
                }

                // The author's own rule, written in CEL. The strongest signal available,
                // because it is the form's author saying what they meant, not a guess.
                var ruleMessage = FormExpressions.Validate(prompt, context);
                if (!string.IsNullOrEmpty(ruleMessage))
                {
                    Flag("RULE_FAILED", ReviewSeverity.NeedsReview,
                        $"The form's own rule reports: {ruleMessage}");
                }

                // Type and pattern advisories, from the shared inspector so the CLI, the
                // desktop app and this report never disagree about what counts.
                foreach (var warning in typeInspector.ValidateResponse(prompt).Warnings)
                {
                    Flag(warning.WarningCode ?? "HINT_MISMATCH", ReviewSeverity.NeedsReview, warning.Message);
                }

                var hints = prompt.Hints;
                if (hints is null)
                {
                    continue;
                }

                if (hints.SuggestedValues.Count > 0 &&
                    !hints.SuggestedValues.Contains(response, StringComparer.Ordinal))
                {
                    Flag("OUTSIDE_SUGGESTED", ReviewSeverity.Advisory,
                        $"Not one of the {hints.SuggestedValues.Count} suggested options. " +
                        "The format allows this and it is often the right answer.");
                }

                foreach (var (code, message) in OutOfBounds(response, hints))
                {
                    Flag(code, ReviewSeverity.Advisory, message);
                }
            }

            foreach (var child in section.Sections)
            {
                Walk(child, here);
            }
        }

        foreach (var section in document.Sections)
        {
            Walk(section, string.Empty);
        }

        var verdict = findings.Any(f => f.Severity == ReviewSeverity.NeedsReview)
            ? ReviewVerdict.ReviewRequired
            : findings.Count > 0
                ? ReviewVerdict.ReviewRecommended
                : ReviewVerdict.Processable;

        return new DocumentReview
        {
            Findings = findings,
            Verdict = verdict,
            PromptsConsidered = considered,
            BlankCount = blanks,
        };
    }

    private static bool IsTemporal(string? declared) =>
        declared is "date" or "time" or "datetime";

    private static IEnumerable<(string Code, string Message)> OutOfTemporalBounds(
        string response, PromptHints hints)
    {
        if (!DateTimeOffset.TryParse(response, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var when))
        {
            yield break;   // Not a date at all; the type inspector already said so.
        }

        if (DateTimeOffset.TryParse(hints.Min, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var earliest)
            && when < earliest)
        {
            yield return ("OUTSIDE_BOUNDS",
                $"Earlier than the suggested earliest value of {hints.Min}. Bounds describe " +
                "the control offered, not a limit on the answer (specification 4.7).");
        }

        if (DateTimeOffset.TryParse(hints.Max, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var latest)
            && when > latest)
        {
            yield return ("OUTSIDE_BOUNDS",
                $"Later than the suggested latest value of {hints.Max}. Bounds describe " +
                "the control offered, not a limit on the answer (specification 4.7).");
        }
    }

    /// <summary>Bounds are an offer, so falling outside one is advisory and nothing more.</summary>
    private static IEnumerable<(string Code, string Message)> OutOfBounds(string response, PromptHints hints)
    {
        // On date, time and datetime, min and max are the earliest and latest suggested
        // values (specification 4.7), so they compare chronologically rather than
        // numerically. Comparing them as numbers silently checked nothing.
        if (IsTemporal(hints.ExpectedDataType))
        {
            foreach (var finding in OutOfTemporalBounds(response, hints))
            {
                yield return finding;
            }
            yield break;
        }

        if (!double.TryParse(response, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
        {
            // Not a number. If that contradicts the declared type the inspector has
            // already said so; repeating it here would double-count one problem.
            yield break;
        }

        if (double.TryParse(hints.Min, NumberStyles.Any, CultureInfo.InvariantCulture, out var min)
            && value < min)
        {
            yield return ("OUTSIDE_BOUNDS",
                $"Below the suggested minimum of {hints.Min}. Bounds describe the control " +
                "offered, not a limit on the answer (specification 4.7).");
        }

        if (double.TryParse(hints.Max, NumberStyles.Any, CultureInfo.InvariantCulture, out var max)
            && value > max)
        {
            yield return ("OUTSIDE_BOUNDS",
                $"Above the suggested maximum of {hints.Max}. Bounds describe the control " +
                "offered, not a limit on the answer (specification 4.7).");
        }
    }
}
