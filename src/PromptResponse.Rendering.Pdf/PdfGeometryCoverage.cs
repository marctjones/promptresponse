using Excise.Core.Document;

namespace PromptResponse.Rendering.Pdf;

/// <summary>One field a converter produced, and where it decided the answer goes.</summary>
/// <param name="PromptId">The APR prompt id the converter invented.</param>
/// <param name="PageNumber">1-based page the target sits on.</param>
/// <param name="Rect">The target's rectangle in PDF user space.</param>
public sealed record PlacedPrompt(string PromptId, int PageNumber, PdfRectangle Rect);

/// <summary>A declared field and the prompt that was placed on it.</summary>
/// <param name="FieldName">The source PDF's fully qualified field name.</param>
/// <param name="PromptId">The id the converter invented for it.</param>
/// <remarks>
/// The pairing that geometry establishes, kept rather than discarded. A
/// converter reading a flattened form invents its own ids, so without this the
/// only thing knowable is <em>how many</em> fields it placed correctly. With it,
/// a field matched by position can then be graded on its <em>label</em> against
/// the source's <c>/TU</c> text — which is what makes a flat fixture gradable
/// end to end rather than on placement alone.
/// </remarks>
public sealed record GeometryMatch(string FieldName, string PromptId);

/// <summary>
/// Grades a conversion against a source PDF's widget rectangles, matching on
/// <em>where</em> each field is rather than what it is called.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PdfWidgetManifest.Compare"/> matches a prompt id against a field's
/// fully qualified name. That is exact and free for an AcroForm import, where
/// the id <em>is</em> the field name — and useless for the case the derived
/// fixtures were built for. A converter reading <c>fed-w9-flat</c> has no access
/// to <c>fed-w9</c>'s field names; it invents ids, and name matching reports
/// every field missing. The answer keys exist but that key does not turn.
/// </para>
/// <para>
/// Geometry is the key that does. Flattening removes the widgets but not the
/// page: the blank a person writes on is in the same place it always was, so a
/// converter that found the right blank can be graded against the widget that
/// used to sit there — with no human, no model, and no answer key to maintain.
/// </para>
/// <para>
/// It also grades the right thing. A converter can attach a perfectly good label
/// to the wrong input; name or label matching cannot see that, and geometry
/// scores placement independently of naming.
/// </para>
/// <para>
/// <b>There is a chance floor, and it is not small.</b> Government forms share a
/// page size and standard line spacing, so one form's field positions land on
/// another's by coincidence. Measured across five corpus forms, offering the
/// wrong form's layout as a conversion covers <b>0-48%</b> of the target's
/// fields — and the floor tracks how <em>many</em> fields the wrong form has
/// rather than how similar it is, because more candidates means more accidental
/// hits.
/// </para>
/// <para>
/// So <see cref="WidgetCoverage.Fraction"/> is not a score on its own: a
/// converter can raise it by emitting more guesses. Always read it with
/// <see cref="WidgetCoverage.UnaccountedPromptIds"/>, which is the precision
/// half — in the measured cases above, most of what the wrong form offered
/// matched nothing at all, and that is what a caller looking only at coverage
/// would miss.
/// </para>
/// </remarks>
public static class PdfGeometryCoverage
{
    /// <summary>
    /// How much of the smaller rectangle must fall inside the larger for a match.
    /// </summary>
    /// <remarks>
    /// Measured against <c>fed-w9</c>, whose widgets are 8-38pt tall with a
    /// median of 14. A converter reading the printed page finds the <em>drawn
    /// rule</em> — roughly 1pt tall and sitting on the widget's baseline — so the
    /// two rectangles describing one field differ in area by an order of
    /// magnitude even when the answer is perfectly right. Intersection over
    /// <em>union</em> scores that ideal case at <b>0.071</b> and would reject it;
    /// intersection over the smaller area scores it 1.0. That is why the metric
    /// is over-minimum, and it is a correction to this file's first version.
    /// </remarks>
    public const double DefaultMinimumOverlap = 0.5;

    /// <summary>
    /// How far outside a widget the candidate's centre may sit, in points.
    /// </summary>
    /// <remarks>
    /// Overlap-over-minimum alone is satisfied by anything that merely
    /// <em>contains</em> the widget — a page-sized rectangle scores 1.0 against
    /// every field on the page, which would make a converter that emitted one
    /// box per page look perfect. Requiring the candidate's centre to lie at the
    /// field rules that out (a page's centre is not inside a given widget) while
    /// still accepting a rule drawn just below the widget's lower edge, which is
    /// where form rules usually sit.
    /// </remarks>
    public const double DefaultCentreTolerance = 4.0;

    /// <summary>
    /// Compares placed prompts against the widgets a source PDF declares.
    /// </summary>
    /// <param name="manifest">The source PDF's widget manifest — the answer key.</param>
    /// <param name="placed">What the converter produced, with page and rectangle.</param>
    /// <param name="minimumOverlap">
    /// Overlap floor for a match, as intersection over the smaller rectangle's
    /// area. Defaults to <see cref="DefaultMinimumOverlap"/>.
    /// </param>
    /// <param name="centreTolerance">
    /// How far outside a widget the candidate's centre may sit. Defaults to
    /// <see cref="DefaultCentreTolerance"/>.
    /// </param>
    /// <param name="offsetY">
    /// Added to every manifest rectangle's vertical position before matching, for
    /// a fixture whose page origin moved relative to its source. The corpus has
    /// exactly one: <c>ct-dmv-a25-flat</c> needs <c>-198</c>, because flattening
    /// baked in its source's CropBox of <c>[0 198 612 585]</c>. Every other
    /// derived fixture preserves its source's geometry and needs no offset.
    /// </param>
    /// <returns>
    /// Coverage naming the fields nothing was placed on, and the prompts that
    /// matched no widget.
    /// </returns>
    public static WidgetCoverage Compare(
        WidgetManifest manifest,
        IReadOnlyList<PlacedPrompt> placed,
        double minimumOverlap = DefaultMinimumOverlap,
        double offsetY = 0,
        double centreTolerance = DefaultCentreTolerance)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(placed);

        // Only placed widgets can be matched geometrically. A field with no
        // rectangle (a non-terminal field-tree node) is not evidence of a
        // converter failure, so it is excluded from the denominator rather than
        // counted as missing -- otherwise the score would punish a converter for
        // something it could not have found.
        var expected = manifest.Importable
            .Where(e => e.Rect is not null && e.PageNumber is not null)
            .ToList();

        var unmatched = new List<PlacedPrompt>(placed);
        var missing = new List<string>();
        var matched = new List<GeometryMatch>();

        foreach (var widget in expected)
        {
            var target = Shift(widget.Rect!.Value, offsetY);

            // Greedy best-overlap. Each placed prompt is consumed by at most one
            // widget, so two prompts on one blank cannot satisfy two widgets.
            var best = -1;
            var bestOverlap = minimumOverlap;
            for (var i = 0; i < unmatched.Count; i++)
            {
                if (unmatched[i].PageNumber != widget.PageNumber)
                {
                    continue;
                }

                var candidate = unmatched[i].Rect;
                if (!CentreLiesAt(candidate, target, centreTolerance))
                {
                    continue;
                }

                var overlap = Overlap(target, candidate);
                if (overlap >= bestOverlap)
                {
                    best = i;
                    bestOverlap = overlap;
                }
            }

            if (best >= 0)
            {
                matched.Add(new GeometryMatch(widget.FullName, unmatched[best].PromptId));
                unmatched.RemoveAt(best);
            }
            else
            {
                missing.Add(widget.FullName);
            }
        }

        return new WidgetCoverage(
            expected.Count,
            expected.Count - missing.Count,
            missing,
            [.. unmatched.Select(p => p.PromptId)],
            matched);
    }

    /// <summary>
    /// How much of the smaller rectangle lies inside the larger, 0-1. Reaches 1
    /// when one wholly contains the other, whatever their relative sizes.
    /// </summary>
    public static double Overlap(PdfRectangle a, PdfRectangle b)
    {
        var intersection = IntersectionArea(a, b);
        var smaller = Math.Min(Area(a), Area(b));
        return smaller <= 0 ? 0 : intersection / smaller;
    }

    /// <summary>Overlap of two rectangles as intersection area over union area, 0-1.</summary>
    /// <remarks>
    /// Kept as the stricter alternative, for comparing two rectangles that
    /// genuinely should be the same size. It is <em>not</em> the criterion
    /// <see cref="Compare"/> uses — see <see cref="DefaultMinimumOverlap"/> for
    /// why a widget box and a drawn rule are not that pair.
    /// </remarks>
    public static double IntersectionOverUnion(PdfRectangle a, PdfRectangle b)
    {
        var intersection = IntersectionArea(a, b);
        var union = Area(a) + Area(b) - intersection;
        return union <= 0 ? 0 : intersection / union;
    }

    /// <summary>Whether a candidate is positioned at a widget, rather than merely covering it.</summary>
    private static bool CentreLiesAt(PdfRectangle candidate, PdfRectangle widget, double tolerance)
    {
        var x = (candidate.Left + candidate.Right) / 2;
        var y = (candidate.Bottom + candidate.Top) / 2;
        return x >= widget.Left - tolerance && x <= widget.Right + tolerance
            && y >= widget.Bottom - tolerance && y <= widget.Top + tolerance;
    }

    private static double IntersectionArea(PdfRectangle a, PdfRectangle b)
    {
        var left = Math.Max(a.Left, b.Left);
        var right = Math.Min(a.Right, b.Right);
        var bottom = Math.Max(a.Bottom, b.Bottom);
        var top = Math.Min(a.Top, b.Top);
        return right <= left || top <= bottom ? 0 : (right - left) * (top - bottom);
    }

    private static double Area(PdfRectangle r) =>
        Math.Max(0, r.Right - r.Left) * Math.Max(0, r.Top - r.Bottom);

    private static PdfRectangle Shift(PdfRectangle r, double dy) =>
        dy == 0 ? r : new PdfRectangle(r.Left, r.Bottom + dy, r.Right, r.Top + dy);
}
