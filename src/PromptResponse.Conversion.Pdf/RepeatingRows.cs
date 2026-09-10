namespace PromptResponse.Conversion.Pdf;

/// <summary>A run of fields that are the same question asked once per row.</summary>
/// <param name="PageNumber">1-based page.</param>
/// <param name="FieldIds">The fields, top of the page first.</param>
/// <param name="Pitch">Vertical distance between rows, in points.</param>
public sealed record RepeatingColumn(int PageNumber, IReadOnlyList<string> FieldIds, double Pitch);

/// <summary>
/// Finds fields that are rows of a repeating group rather than separate questions.
/// </summary>
/// <remarks>
/// <para>
/// A form prints a caption once and repeats the row beneath it. I-9's Supplement
/// B is three such rows at a pitch of 186pt, and "Signature of Employer or
/// Authorized Representative" is printed once, 2pt above the first row's field.
/// </para>
/// <para>
/// One rule breaks that: a run may label only one field. The first row takes the
/// caption and the other two get nothing, and no distance rule can help, because
/// the caption is 188 and 374pt from the fields that still need it. A caption
/// above a repeating group legitimately labels every row of it.
/// </para>
/// <para>
/// <b>Keyed off the fields, not the ink.</b> Detecting a table from its drawn
/// rules would fire on W-9's pages 3 to 6, which are dense instruction tables
/// containing no fields at all and already the source of most of the converter's
/// false positives. Fields cannot repeat where there are none.
/// </para>
/// </remarks>
public static class RepeatingRows
{
    /// <summary>How close two fields' left edges and widths must be to be one column, in points.</summary>
    public const double ColumnTolerance = 4.0;

    /// <summary>How much two row gaps may differ and still count as one pitch, in points.</summary>
    public const double PitchTolerance = 3.0;

    /// <summary>The shortest gap that can be a row pitch, in points.</summary>
    /// <remarks>
    /// Below this, "rows" are the lines of one block rather than repeats of it.
    /// I-9's real pitch is 186pt; its within-block field spacing is 20-60pt.
    /// </remarks>
    public const double MinimumPitch = 60.0;

    /// <summary>Rows a column needs before it is a repeat rather than a coincidence.</summary>
    public const int MinimumRows = 3;

    /// <summary>Columns that must agree on the pitch before any of them is believed.</summary>
    /// <remarks>
    /// One column of evenly spaced fields is just a form with regular line
    /// spacing. Several columns agreeing on one pitch is a repeating group.
    /// </remarks>
    public const int MinimumAgreeingColumns = 2;

    /// <summary>Finds the repeating columns among a page's fields.</summary>
    public static IReadOnlyList<RepeatingColumn> Detect(IReadOnlyList<DiscoveredField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var found = new List<RepeatingColumn>();

        foreach (var page in fields.Where(f => f.TargetRect is not null).GroupBy(f => f.PageNumber))
        {
            var candidates = new List<RepeatingColumn>();

            foreach (var column in Cluster([.. page]))
            {
                var ordered = column.OrderByDescending(f => f.TargetRect!.Value.Bottom).ToList();
                if (ordered.Count < MinimumRows)
                {
                    continue;
                }

                var gaps = ordered
                    .Zip(ordered.Skip(1), (a, b) => a.TargetRect!.Value.Bottom - b.TargetRect!.Value.Bottom)
                    .ToList();

                var pitch = gaps[0];
                if (pitch < MinimumPitch || gaps.Any(g => Math.Abs(g - pitch) > PitchTolerance))
                {
                    continue;
                }

                candidates.Add(new RepeatingColumn(page.Key, [.. ordered.Select(f => f.Id)], pitch));
            }

            found.AddRange(candidates
                .GroupBy(c => Math.Round(c.Pitch / PitchTolerance))
                .Where(g => g.Count() >= MinimumAgreeingColumns)
                .SelectMany(g => g));
        }

        return found;
    }

    /// <summary>How far apart two fields on one line may sit and still be one entry, in points.</summary>
    /// <remarks>
    /// W-9 splits a social security number into three boxes separated by the
    /// printed dashes, 14.4pt apart, and an EIN into two the same way. They are
    /// one question with one caption above the group, so the caption has to
    /// reach all of them.
    /// </remarks>
    public const double AdjacentFieldGap = 20.0;

    /// <summary>
    /// Finds runs of adjacent fields sharing one line, which one caption above
    /// the group labels.
    /// </summary>
    /// <remarks>
    /// The horizontal counterpart of <see cref="Detect"/>. Without it the first
    /// box of W-9's SSN takes "Social security number" and the other two get
    /// nothing, because a run may otherwise label only one field.
    /// </remarks>
    public static IReadOnlyList<RepeatingColumn> DetectRows(IReadOnlyList<DiscoveredField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var found = new List<RepeatingColumn>();

        foreach (var page in fields.Where(f => f.TargetRect is not null).GroupBy(f => f.PageNumber))
        {
            // Fields sharing a line: same top and bottom, to the point.
            foreach (var band in page.GroupBy(f => (
                         Bottom: Math.Round(f.TargetRect!.Value.Bottom),
                         Top: Math.Round(f.TargetRect!.Value.Top))))
            {
                var ordered = band.OrderBy(f => f.TargetRect!.Value.Left).ToList();
                if (ordered.Count < 2)
                {
                    continue;
                }

                // Split the band wherever the fields stop touching: two entries
                // at opposite ends of a row are not one question.
                var run = new List<DiscoveredField> { ordered[0] };
                for (var i = 1; i <= ordered.Count; i++)
                {
                    var breaks = i == ordered.Count
                        || ordered[i].TargetRect!.Value.Left - ordered[i - 1].TargetRect!.Value.Right > AdjacentFieldGap;

                    if (breaks)
                    {
                        if (run.Count >= 2)
                        {
                            found.Add(new RepeatingColumn(page.Key, [.. run.Select(f => f.Id)], 0));
                        }

                        if (i < ordered.Count)
                        {
                            run = [ordered[i]];
                        }
                    }
                    else
                    {
                        run.Add(ordered[i]);
                    }
                }
            }
        }

        return found;
    }

    /// <summary>
    /// Groups fields whose left edge and width are within
    /// <see cref="ColumnTolerance"/> of each other.
    /// </summary>
    /// <remarks>
    /// Clustered by nearness, <b>not</b> by rounding into buckets. Rounding was
    /// the first version and it silently missed the case this exists for: I-9's
    /// three "Signature of Employer or Authorized Representative" fields are
    /// 198, 199 and 197pt wide, and at a 4pt bucket 197 rounds into the
    /// neighbouring bucket while 198 and 199 do not. The column split into a
    /// pair and a single, both fell under the row minimum, and the detector
    /// reported nothing — which measured as "repeating rows change no outcome"
    /// and cost the whole idea a deletion before the bug was found.
    /// </remarks>
    private static List<List<DiscoveredField>> Cluster(List<DiscoveredField> fields)
    {
        var clusters = new List<List<DiscoveredField>>();

        foreach (var field in fields.OrderBy(f => f.TargetRect!.Value.Left))
        {
            var rect = field.TargetRect!.Value;
            var width = rect.Right - rect.Left;
            var into = clusters.FirstOrDefault(c =>
                Math.Abs(c[0].TargetRect!.Value.Left - rect.Left) <= ColumnTolerance
                && Math.Abs(c[0].TargetRect!.Value.Right - c[0].TargetRect!.Value.Left - width) <= ColumnTolerance);

            if (into is null)
            {
                clusters.Add([field]);
            }
            else
            {
                into.Add(field);
            }
        }

        return clusters;
    }
}
