namespace PromptResponse.Conversion.Pdf;

/// <summary>How well one recovered label matches the form author's own words.</summary>
/// <param name="FieldId">The field being scored.</param>
/// <param name="Recovered">What label recovery found on the page.</param>
/// <param name="Expected">The form author's <c>/TU</c> text, held back from the pipeline.</param>
/// <param name="Score">
/// Fraction of the recovered label's distinctive words that the author also
/// used, 0-1. Precision, not recall — see <see cref="LabelAgreement"/>.
/// </param>
public sealed record LabelMatch(string FieldId, string? Recovered, string Expected, double Score)
{
    /// <summary>Whether this counts as having found the right question.</summary>
    public bool IsAgreement => Score >= LabelAgreement.AgreementThreshold;
}

/// <summary>What a whole form scored.</summary>
/// <param name="FormId">The form.</param>
/// <param name="Gradable">Fields carrying an author label to be graded against.</param>
/// <param name="Attempted">Of those, the ones recovery produced any label for.</param>
/// <param name="Agreed">Of those, the ones whose label matches the author's.</param>
/// <param name="Matches">Every field's result, for looking at what went wrong.</param>
/// <param name="ChanceFloor">
/// The same score computed against the <em>next</em> field's expected label. A
/// metric that cannot tell a field's own label from its neighbour's is measuring
/// boilerplate, not agreement.
/// </param>
public sealed record FormLabelAgreement(
    string FormId,
    int Gradable,
    int Attempted,
    int Agreed,
    IReadOnlyList<LabelMatch> Matches,
    double ChanceFloor)
{
    /// <summary>Of the fields that could be graded, the fraction recovered correctly.</summary>
    public double Recall => Gradable == 0 ? 0 : (double)Agreed / Gradable;

    /// <summary>Of the labels recovery actually produced, the fraction that are right.</summary>
    public double Precision => Attempted == 0 ? 0 : (double)Agreed / Attempted;
}

/// <summary>
/// Grades recovered labels against the form author's own <c>/TU</c> text.
/// </summary>
/// <remarks>
/// <para>
/// Label recovery had no mechanical oracle: a field's label is not stated
/// anywhere a program can read it, so "is this the right question" needed a
/// person. That is true for most of the corpus — and not for the 152 fields
/// whose author wrote a tooltip. For those, the right answer <em>is</em>
/// recorded in the PDF, by the person who made the form.
/// </para>
/// <para>
/// So the oracle is: hide the tooltips
/// (<see cref="ConversionPipeline.WithoutFormAuthorLabels"/>), make recovery
/// find the question on the page, and compare. No human, no model, and 128
/// checks on <c>fed-i9</c> alone.
/// </para>
/// <para>
/// <b>Scored as precision, not recall.</b> A tooltip carries context the page
/// never prints: I-9 writes "Section 1., Enter First Name (Given Name)." where
/// the page prints only "First Name (Given Name)". Requiring recovery to
/// reproduce the whole tooltip would fail a perfect answer, so what is measured
/// is whether the words recovery <em>did</em> find are the author's.
/// </para>
/// <para>
/// <b>Boilerplate is removed before scoring.</b> Nearly every I-9 tooltip
/// contains "Section", "Enter" and a number; leaving them in would let any run
/// of text match anything. A word appearing in more than half a form's tooltips
/// carries no information about which field is which, so it is struck out.
/// </para>
/// </remarks>
public static class LabelAgreement
{
    /// <summary>Fraction of distinctive words that must match for a label to count as right.</summary>
    /// <remarks>
    /// Deliberately below 1: the page and the tooltip are different phrasings of
    /// the same question, not the same string. Half the distinctive words is
    /// enough to establish that recovery found the right question and not the
    /// one next to it, which is what the chance floor checks.
    /// </remarks>
    public const double AgreementThreshold = 0.5;

    /// <summary>A word in more than this fraction of a form's tooltips is boilerplate.</summary>
    public const double BoilerplateFraction = 0.5;

    /// <summary>Distinctive words a recovered label must have before it can be scored.</summary>
    public const int MinimumDistinctiveWords = 2;

    /// <summary>Grades one form's recovered labels against its held-back tooltips.</summary>
    /// <param name="formId">The form, for reporting.</param>
    /// <param name="expected">Field id to the author's own label.</param>
    /// <param name="recovered">Field id to what recovery produced, where it produced anything.</param>
    public static FormLabelAgreement Compare(
        string formId,
        IReadOnlyList<(string FieldId, string Expected)> expected,
        IReadOnlyDictionary<string, string?> recovered)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(recovered);

        var boilerplate = Boilerplate(expected.Select(e => e.Expected));

        var matches = expected
            .Select(e => new LabelMatch(
                e.FieldId,
                recovered.GetValueOrDefault(e.FieldId),
                e.Expected,
                Score(recovered.GetValueOrDefault(e.FieldId), e.Expected, boilerplate)))
            .ToList();

        // The control: score each field's recovered label against the NEXT
        // field's expected label. If a shifted alignment scores as well as the
        // real one, the metric is matching boilerplate rather than meaning.
        var shifted = expected.Count < 2 ? 0 : Enumerable
            .Range(0, expected.Count)
            .Select(i => Score(
                recovered.GetValueOrDefault(expected[i].FieldId),
                expected[(i + 1) % expected.Count].Expected,
                boilerplate))
            .Count(s => s >= AgreementThreshold) / (double)expected.Count;

        return new FormLabelAgreement(
            formId,
            expected.Count,
            matches.Count(m => !string.IsNullOrWhiteSpace(m.Recovered)),
            matches.Count(m => m.IsAgreement),
            matches,
            shifted);
    }

    /// <summary>Words that appear in enough of a form's labels to say nothing about any one of them.</summary>
    public static IReadOnlySet<string> Boilerplate(IEnumerable<string> labels)
    {
        var all = labels.ToList();
        if (all.Count == 0)
        {
            return new HashSet<string>();
        }

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var label in all)
        {
            foreach (var word in Words(label).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                counts[word] = counts.GetValueOrDefault(word) + 1;
            }
        }

        return counts
            .Where(kv => kv.Value > all.Count * BoilerplateFraction)
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// What fraction of a recovered label's distinctive words the author also used.
    /// </summary>
    public static double Score(string? recovered, string expected, IReadOnlySet<string> boilerplate)
    {
        if (string.IsNullOrWhiteSpace(recovered))
        {
            return 0;
        }

        // An exact match is never an accident, so it is not subject to the
        // two-distinctive-word floor below. Without this the oracle marks
        // verbatim agreement as failure whenever the label is short: on fed-w9
        // it scored "C corporation" against "C corporation" as WRONG, because
        // "C" is one letter and "corporation" is then the only token left.
        if (Normalise(recovered).Equals(Normalise(expected), StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        var found = Distinctive(recovered, boilerplate);
        var author = Distinctive(expected, boilerplate).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Too little to judge. A one-word label that happens to be the author's
        // word is not evidence the right question was found.
        if (found.Count < MinimumDistinctiveWords || author.Count == 0)
        {
            return 0;
        }

        return found.Count(w => author.Contains(w)) / (double)found.Count;
    }

    /// <summary>Trims and collapses whitespace and trailing punctuation for exact comparison.</summary>
    private static string Normalise(string text) =>
        string.Join(' ', text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
            .Trim(' ', '.', ':', ',', ';');

    private static List<string> Distinctive(string text, IReadOnlySet<string> boilerplate) =>
        [.. Words(text).Where(w => !boilerplate.Contains(w)).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Content words, lowercased, punctuation and digits stripped.</summary>
    private static IEnumerable<string> Words(string text) =>
        text.Split([' ', ',', '.', '(', ')', '/', ':', ';', '-', '’', '\''],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => w.ToLowerInvariant())
            .Where(w => w.Count(char.IsLetter) >= 2);
}
