using Excise.Core.Document;

namespace PromptResponse.Conversion.Pdf;

/// <summary>
/// Finds one answer that a form printed as several boxes, and joins it back up.
/// </summary>
/// <remarks>
/// <para>
/// W-9 asks for a social security number once and prints three boxes for it,
/// separated by the dashes that belong in the number. Emitting three prompts
/// asks a person the same question three times, which is a print-layout
/// artefact leaking into a format that has no layout.
/// </para>
/// <para>
/// <b>The signal is the printed separator, not adjacency.</b> Adjacent fields
/// sharing a label are usually NOT one answer: SS-4 puts "City, state, and ZIP
/// code" on lines 4b and 5b, and I-9 repeats "Issuing Authority" down its List
/// A, B and C rows. Those are different answers to the same question and must
/// stay apart. What distinguishes a genuinely split value is that the form
/// draws the separator between the pieces — W-9's extraction yields "– –"
/// between the social security boxes and "–" between the employer
/// identification ones — because that punctuation belongs to the value.
/// </para>
/// </remarks>
public static class SplitEntries
{
    /// <summary>How far apart two boxes of one value may sit, in points.</summary>
    public const double MaximumPartGap = 24.0;

    /// <summary>Characters a form prints between the pieces of one value.</summary>
    public const string SeparatorCharacters = "-–—/.";

    /// <summary>Joins the boxes of each split value into a single field.</summary>
    /// <param name="fields">The fields, after labels have been recovered.</param>
    /// <param name="spans">
    /// The page's text, used to find the separator printed between two boxes.
    /// </param>
    public static IReadOnlyList<DiscoveredField> Merge(
        IReadOnlyList<DiscoveredField> fields,
        IReadOnlyList<TextSpan> spans)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(spans);

        var merged = new List<DiscoveredField>();
        var consumed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var band in fields
                     .Where(f => f.TargetRect is not null)
                     .GroupBy(f => (f.PageNumber,
                         Bottom: Math.Round(f.TargetRect!.Value.Bottom),
                         Top: Math.Round(f.TargetRect!.Value.Top))))
        {
            var ordered = band.OrderBy(f => f.TargetRect!.Value.Left).ToList();
            var run = new List<DiscoveredField> { ordered[0] };

            for (var i = 1; i <= ordered.Count; i++)
            {
                var joins = i < ordered.Count && Joins(run[^1], ordered[i], band.Key.PageNumber, spans);
                if (joins)
                {
                    run.Add(ordered[i]);
                    continue;
                }

                if (run.Count > 1)
                {
                    merged.Add(Join(run));
                    foreach (var part in run)
                    {
                        consumed.Add(part.Id);
                    }
                }

                if (i < ordered.Count)
                {
                    run = [ordered[i]];
                }
            }
        }

        // Order is preserved: a merged field takes the position of its first
        // part, so the questions still come in the order the form asks them.
        var byId = merged.ToDictionary(m => m.Id, StringComparer.Ordinal);
        return
        [
            .. fields
                .Where(f => !consumed.Contains(f.Id) || byId.ContainsKey(f.Id))
                .Select(f => byId.TryGetValue(f.Id, out var m) ? m : f),
        ];
    }

    /// <summary>Whether two boxes on one line are pieces of a single value.</summary>
    private static bool Joins(DiscoveredField left, DiscoveredField right, int page, IReadOnlyList<TextSpan> spans)
    {
        var a = left.TargetRect!.Value;
        var b = right.TargetRect!.Value;

        var gap = b.Left - a.Right;
        if (gap < 0 || gap > MaximumPartGap)
        {
            return false;
        }

        // One value carries one question. Two boxes with different labels are
        // two questions that happen to sit side by side.
        if (!left.HasLabel || !right.HasLabel
            || !string.Equals(left.Label, right.Label, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // The form has to print the separator in the space between them. The
        // run only has to REACH INTO that space, not sit wholly inside it:
        // extraction gives W-9's social security dashes as a single run "– –"
        // spanning both gaps at once, so a containment test joins nothing.
        return spans.Any(s => s.PageNumber == page
            && s.Rect.Right > a.Right - 1
            && s.Rect.Left < b.Left + 1
            && s.Rect.Bottom < a.Top
            && s.Rect.Top > a.Bottom
            && s.Text.Trim().Length > 0
            && s.Text.Trim().All(c => SeparatorCharacters.Contains(c) || char.IsWhiteSpace(c)));
    }

    private static DiscoveredField Join(List<DiscoveredField> parts)
    {
        var rects = parts.Select(p => p.TargetRect!.Value).ToList();
        return parts[0] with
        {
            TargetRect = new PdfRectangle(
                rects.Min(r => r.Left), rects.Min(r => r.Bottom),
                rects.Max(r => r.Right), rects.Max(r => r.Top)),
            Parts = [.. parts.Select(p => new FieldPart(p.Id, p.TargetRect!.Value))],
        };
    }
}
