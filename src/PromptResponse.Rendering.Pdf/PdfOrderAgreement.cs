using PromptResponse.Core.Models;

namespace PromptResponse.Rendering.Pdf;

/// <summary>
/// How well a conversion's question order matches the order the form asks them in.
/// </summary>
/// <param name="Comparable">Prompts matched to a reference field, and so orderable.</param>
/// <param name="Concordant">Pairs the conversion put in the same relative order as the form.</param>
/// <param name="Discordant">Pairs it put in the opposite order.</param>
/// <param name="FirstInversion">
/// The first pair found out of order, as a human-readable "expected A before B",
/// or null when there is none. A bare score says a form is jumbled; this says
/// where to look.
/// </param>
/// <param name="UnmatchedPromptIds">
/// Prompts with no reference field, which cannot be placed in the sequence and
/// so take no part in the score.
/// </param>
public sealed record OrderAgreement(
    int Comparable,
    int Concordant,
    int Discordant,
    string? FirstInversion,
    IReadOnlyList<string> UnmatchedPromptIds)
{
    /// <summary>
    /// Kendall's tau over the comparable prompts: 1 when the order matches, -1
    /// when it is exactly reversed, 0 when it carries no ordering information.
    /// </summary>
    /// <remarks>
    /// A rank correlation rather than a count of misplaced items, because the
    /// question is how badly the sequence is wrong, not merely whether it is. Two
    /// adjacent questions swapped is a small defect; a form asked back to front
    /// is a different kind of failure, and a metric that scored them alike would
    /// hide the distinction.
    /// </remarks>
    public double Tau => Concordant + Discordant == 0
        ? 1.0
        : (double)(Concordant - Discordant) / (Concordant + Discordant);

    /// <summary>Every comparable pair is in the order the form asks them.</summary>
    public bool IsInOrder => Discordant == 0;
}

/// <summary>
/// Checks that a conversion asks the form's questions in the form's order.
/// </summary>
/// <remarks>
/// Field completeness comparisons are multisets, so they are order-blind by
/// construction: a W-9 whose every question is asked backwards scores a perfect
/// 1.00 for coverage. That is fine for "did we lose a field" and useless for a
/// document a person fills in from top to bottom, where sequence carries meaning
/// — "if you checked box 3a" is nonsense before 3a has been asked.
/// <para>
/// The reference order this grades against is <em>geometric</em> (down the page,
/// then across), which is right for a single-column form and approximate for a
/// multi-column one. Read a low score as a prompt to look, not as proof of a bug.
/// </para>
/// </remarks>
public static class PdfOrderAgreement
{
    /// <summary>Compares a converted document's prompt order against a reference.</summary>
    /// <remarks>
    /// Prompts are matched to reference fields by name, which holds for an
    /// AcroForm import because the prompt id is the field name. A converter that
    /// invents ids needs the geometry-matched overload once it can place its
    /// fields.
    /// </remarks>
    public static OrderAgreement Compare(
        PdfStructuredReference reference,
        AprDocument document,
        bool useDeclarationOrder = false)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(document);

        var expectedOrder = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var field in reference.Fields)
        {
            expectedOrder.TryAdd(field.Name, useDeclarationOrder ? field.DeclarationOrder : field.Order);
        }

        var sequence = new List<(string Id, int Expected)>();
        var unmatched = new List<string>();

        foreach (var prompt in document.Sections.SelectMany(s => s.Prompts))
        {
            var name = StripDisambiguator(prompt.Id);
            if (expectedOrder.TryGetValue(name, out var order))
            {
                sequence.Add((prompt.Id, order));
            }
            else
            {
                unmatched.Add(prompt.Id);
            }
        }

        var concordant = 0;
        var discordant = 0;
        string? firstInversion = null;

        for (var i = 0; i < sequence.Count; i++)
        {
            for (var j = i + 1; j < sequence.Count; j++)
            {
                if (sequence[i].Expected == sequence[j].Expected)
                {
                    continue;
                }

                if (sequence[i].Expected < sequence[j].Expected)
                {
                    concordant++;
                }
                else
                {
                    discordant++;
                    firstInversion ??=
                        $"the form asks '{sequence[j].Id}' before '{sequence[i].Id}', " +
                        $"but the conversion asks them the other way round";
                }
            }
        }

        return new OrderAgreement(sequence.Count, concordant, discordant, firstInversion, unmatched);
    }

    private static string StripDisambiguator(string id)
    {
        var hash = id.LastIndexOf('#');
        return hash > 0 && id.AsSpan(hash + 1).ToString() is { Length: > 0 } tail && tail.All(char.IsDigit)
            ? id[..hash]
            : id;
    }
}
