using PromptResponse.Core.Models;

namespace PromptResponse.Core.Review;

/// <summary>How a submitted form differs from the template it claims to answer.</summary>
public sealed class FormComparison
{
    /// <summary>
    /// True when the submission's form definition is byte-identical to the template's.
    /// </summary>
    /// <remarks>
    /// The comparison uses the beta.6 structural definition: titles, ids, ordered
    /// structure, labels and hints, excluding responses. So a true here means the submitter answered
    /// exactly the questions that were asked, and answering them changed nothing.
    /// </remarks>
    public bool DefinitionIdentical { get; init; }

    /// <summary>Whether the submission's declared templateId and version match the template's.</summary>
    /// <remarks>
    /// Self-asserted, and worth exactly what that implies. It catches the wrong form sent
    /// by accident; it catches nothing sent deliberately, because whoever changed the
    /// questions could equally change the label on the tin.
    /// </remarks>
    public bool IdentityMatches { get; init; }

    /// <summary>What differs, field by field.</summary>
    public IReadOnlyList<ReviewFinding> Findings { get; init; } = [];
}

/// <summary>
/// Compares a submission against the template it claims to answer.
/// </summary>
/// <remarks>
/// <para>
/// A receiver holding a filled form has a question the form itself cannot answer: is this
/// the form I published? A submission can be perfectly valid, parse cleanly, and still be
/// a different form, an older version, or the right form with the questions edited.
/// </para>
/// <para>
/// The sharpest case, and the one that motivates this, is a prompt keeping its id while
/// its label changes. A pipeline maps responses by id, so it will happily file the answer
/// under the right field - but the person answered a different question. Nothing about the
/// response looks wrong. Only the template says otherwise.
/// </para>
/// </remarks>
public static class FormComparer
{
    /// <summary>Compares a submission against its template.</summary>
    public static FormComparison Compare(AprDocument template, AprDocument submission)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(submission);

        var identical = DefinitionEquals(template, submission);

        var identityMatches =
            string.Equals(template.Metadata.TemplateId, submission.Metadata.TemplateId, StringComparison.Ordinal)
            && string.Equals(template.Metadata.TemplateVersion, submission.Metadata.TemplateVersion, StringComparison.Ordinal);

        var findings = new List<ReviewFinding>();

        if (!identityMatches)
        {
            findings.Add(new ReviewFinding(
                submission.Metadata.TemplateId ?? "", "(document)", "(metadata)",
                "TEMPLATE_IDENTITY_MISMATCH", ReviewSeverity.NeedsReview,
                $"Submission claims template '{submission.Metadata.TemplateId}' version " +
                $"'{submission.Metadata.TemplateVersion}'; the template supplied is " +
                $"'{template.Metadata.TemplateId}' version '{template.Metadata.TemplateVersion}'. " +
                "This may simply be the wrong file to compare against.",
                submission.Metadata.TemplateVersion ?? ""));
        }

        if (identical)
        {
            return new FormComparison
            {
                DefinitionIdentical = true,
                IdentityMatches = identityMatches,
                Findings = findings,
            };
        }

        var expected = Index(template);
        var actual = Index(submission);

        foreach (var (id, want) in expected)
        {
            if (!actual.TryGetValue(id, out var got))
            {
                findings.Add(Finding(id, want, "PROMPT_MISSING",
                    $"The template asks '{want.Prompt.Label}' and the submission does not " +
                    "contain this field at all. The form may be an older version, or edited."));
                continue;
            }

            // The dangerous one. The id still matches, so a pipeline keying on id files
            // the answer under the right field - but it is an answer to a different
            // question, and nothing about the response itself looks wrong.
            if (!string.Equals(want.Prompt.Label, got.Prompt.Label, StringComparison.Ordinal))
            {
                findings.Add(Finding(id, got, "PROMPT_RELABELLED",
                    $"Asked as '{got.Prompt.Label}' but the template asks " +
                    $"'{want.Prompt.Label}'. The response answers a different question from " +
                    "the one intended, and mapping it by id would file it as though it did not."));
            }

            var wantType = want.Prompt.Hints?.ExpectedDataType;
            var gotType = got.Prompt.Hints?.ExpectedDataType;
            if (!string.Equals(wantType, gotType, StringComparison.Ordinal))
            {
                findings.Add(Finding(id, got, "PROMPT_RETYPED",
                    $"Declared as '{gotType ?? "(none)"}' but the template declares " +
                    $"'{wantType ?? "(none)"}'. The response may not mean what the pipeline expects."));
            }

            var wantOptions = want.Prompt.Hints?.SuggestedValues ?? [];
            var gotOptions = got.Prompt.Hints?.SuggestedValues ?? [];
            if (!wantOptions.SequenceEqual(gotOptions, StringComparer.Ordinal))
            {
                findings.Add(Finding(id, got, "PROMPT_OPTIONS_CHANGED",
                    "The offered options differ from the template's, so this answer was " +
                    "chosen from a different list than the one published."));
            }
        }

        foreach (var (id, got) in actual.Where(a => !expected.ContainsKey(a.Key)))
        {
            findings.Add(Finding(id, got, "PROMPT_ADDED",
                $"'{got.Prompt.Label}' is not in the template. The submitter is answering a " +
                "question that was never published."));
        }

        return new FormComparison
        {
            DefinitionIdentical = false,
            IdentityMatches = identityMatches,
            Findings = findings,
        };
    }

    private static bool DefinitionEquals(AprDocument left, AprDocument right) =>
        left.Sections.SelectMany(Flatten).Select(section => (section.Id, section.Title, section.Description))
            .SequenceEqual(right.Sections.SelectMany(Flatten).Select(section => (section.Id, section.Title, section.Description)))
        && left.Sections.SelectMany(Flatten).SelectMany(section => section.Prompts)
            .Select(prompt => (prompt.Id, prompt.Label, prompt.Hints?.ExpectedDataType,
                Options: string.Join("\u001f", prompt.Hints?.SuggestedValues ?? [])))
            .SequenceEqual(right.Sections.SelectMany(Flatten).SelectMany(section => section.Prompts)
                .Select(prompt => (prompt.Id, prompt.Label, prompt.Hints?.ExpectedDataType,
                    Options: string.Join("\u001f", prompt.Hints?.SuggestedValues ?? []))));

    private static IEnumerable<Section> Flatten(Section section)
    {
        yield return section;
        foreach (var child in section.Sections.SelectMany(Flatten)) yield return child;
    }

    private static ReviewFinding Finding(string id, (Prompt Prompt, string Path) at, string code, string message) =>
        new(id, at.Prompt.Label, at.Path, code, ReviewSeverity.NeedsReview, message, at.Prompt.Response ?? "");

    private static Dictionary<string, (Prompt Prompt, string Path)> Index(AprDocument document)
    {
        var map = new Dictionary<string, (Prompt, string)>(StringComparer.Ordinal);

        void Walk(Section section, string path)
        {
            var here = string.IsNullOrWhiteSpace(path) ? section.Title : $"{path} / {section.Title}";
            foreach (var prompt in section.Prompts)
            {
                // Duplicate ids are a structural error the validator reports; here the
                // first occurrence simply wins rather than throwing on a document that
                // has already been flagged elsewhere.
                map.TryAdd(prompt.Id, (prompt, here));
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
        return map;
    }
}
