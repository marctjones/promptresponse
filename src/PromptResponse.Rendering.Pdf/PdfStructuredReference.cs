using System.Text.Json;
using System.Text.Json.Serialization;
using Excise.Core.Document;
using PdfeDoc = Excise.Core.Document.PdfDocument;

namespace PromptResponse.Rendering.Pdf;

/// <summary>A rectangle in PDF user space, in a shape that serializes readably.</summary>
/// <remarks>
/// Deliberately not Excise's <c>PdfRectangle</c>: this is written to a file that
/// outlives any package version, so it carries its own plain shape rather than
/// pinning the reference format to a dependency's type.
/// </remarks>
public sealed record ReferenceRect(double Left, double Bottom, double Right, double Top)
{
    /// <summary>Builds a reference rectangle from Excise's.</summary>
    public static ReferenceRect From(PdfRectangle r) =>
        new(Round(r.Left), Round(r.Bottom), Round(r.Right), Round(r.Top));

    // Two decimals is well below the precision any pairing decision needs, and
    // keeps the committed file diffable instead of churning on float noise.
    private static double Round(double v) => Math.Round(v, 2);
}

/// <summary>One field the PDF declares, with everything mechanically knowable about it.</summary>
/// <param name="Order">
/// Position in geometric reading order across the document — down each page,
/// then across. Inferred, and only approximate on a multi-column form.
/// </param>
/// <param name="DeclarationOrder">
/// Position in the order the AcroForm declares its fields, which is the form
/// author's own sequencing and usually the tab order. Stated by the PDF rather
/// than inferred, but not always the order a reader sees: measured across the
/// corpus the two agree with a Kendall tau of 0.55 to 1.00, so neither is
/// authoritative alone and both are recorded.
/// </param>
/// <param name="Name">The fully qualified AcroForm field name.</param>
/// <param name="Type">Text, Button, Choice, Signature.</param>
/// <param name="Page">1-based page.</param>
/// <param name="Rect">Where the answer goes.</param>
/// <param name="Label">
/// The form author's own <c>/TU</c> label, or null. Where present this is the
/// only label answer nobody inferred — the form states it.
/// </param>
/// <param name="OptionCount">Choice options offered.</param>
public sealed record ReferenceField(
    int Order,
    int DeclarationOrder,
    string Name,
    string Type,
    int Page,
    ReferenceRect Rect,
    string? Label,
    int OptionCount);

/// <summary>One line of printed text, with where it sits.</summary>
/// <param name="Page">1-based page.</param>
/// <param name="Order">Position in geometric reading order within the document.</param>
/// <param name="Text">The line's text, words joined by single spaces.</param>
/// <param name="Rect">The line's bounding box.</param>
public sealed record ReferenceLine(int Page, int Order, string Text, ReferenceRect Rect);

/// <summary>A page's dimensions, so a rectangle can be interpreted without the PDF.</summary>
public sealed record ReferencePage(int Number, double Width, double Height);

/// <summary>
/// Everything a PDF states about itself that can be extracted without judgement,
/// in a form a person or a later process can read, diff, and verify against.
/// </summary>
/// <remarks>
/// <para>
/// APR is not a visual format, so a conversion cannot be checked by looking at it.
/// It is checked structurally: are all the fields here, which are missing, which
/// text belongs to which field, and are the questions asked in the right order.
/// That needs the PDF's content as <em>data</em>, which is what this is.
/// </para>
/// <para>
/// <b>What tier each part is, and why it matters.</b> The PDF remains the source
/// of truth; this is its extracted form, and how much that extraction can be
/// trusted differs by field:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Tier 1, definitional</b> — the field list, types, option counts, widget
/// rectangles, page sizes, and the text of each line with its box. The PDF
/// answers these by stating them; no inference is involved, so grading a
/// converter against them is sound.
/// </description></item>
/// <item><description>
/// <b>Tier 1 where present</b> — a field's <c>/TU</c> label, which is the form
/// author's own words. Only about a third of the corpus's fields carry one.
/// </description></item>
/// <item><description>
/// <b>Inferred, and labelled as such</b> — <see cref="ReferenceField.Order"/> and
/// <see cref="ReferenceLine.Order"/>. Reading order is derived geometrically
/// (down the page, then across), which is right for a single-column form and
/// wrong for a two-column one. It is recorded because order is a real property a
/// conversion must get right, and useful even when approximate — but it is a
/// starting point for review, not an oracle to grade against unchecked.
/// </description></item>
/// </list>
/// <para>
/// Nothing here is an opinion about which text labels which field. That pairing
/// is exactly what the converter is being graded on, so deriving it here with the
/// same logic would be grading the converter against itself.
/// </para>
/// </remarks>
/// <param name="FormId">The corpus id of the form this describes.</param>
/// <param name="PageCount">Pages in the document.</param>
/// <param name="Pages">Each page's number and size, in PDF user space.</param>
/// <param name="Fields">Every field the PDF declares, with its geometry.</param>
/// <param name="Lines">Every line of printed text, with its geometry.</param>
/// <param name="AnswerKey">
/// For a derived fixture, the form whose reference states what a converter
/// reading this one should recover; null for a form that is its own key.
/// <para>
/// This is what makes a flattened or scanned fixture gradable. Extraction here
/// is honest about what the file itself states — <c>fed-w9-flat</c> declares no
/// fields, and <c>fed-w9-scan</c> states nothing at all — but that is precisely
/// the case a converter exists to handle, and the source's reference is the
/// mechanical ground truth for it. Recording the link in the file means a test
/// does not have to rediscover it from the corpus manifest.
/// </para>
/// </param>
public sealed record PdfStructuredReference(
    string FormId,
    int PageCount,
    IReadOnlyList<ReferencePage> Pages,
    IReadOnlyList<ReferenceField> Fields,
    IReadOnlyList<ReferenceLine> Lines,
    string? AnswerKey = null)
{
    /// <summary>Fields carrying the form author's own label.</summary>
    [JsonIgnore]
    public int LabelledFieldCount => Fields.Count(f => !string.IsNullOrWhiteSpace(f.Label));
}

/// <summary>Extracts a <see cref="PdfStructuredReference"/> from a PDF.</summary>
public static class PdfReferenceExtractor
{
    /// <summary>
    /// How close two words' baselines must be to count as the same line, in points.
    /// </summary>
    /// <remarks>
    /// Grouping words into lines is geometric rather than interpretive, but it is
    /// not free of choices: this is the one. Body text on the corpus forms runs
    /// 6-10pt, so words on one line share a baseline to well under a point, while
    /// the next line is a full leading away.
    /// </remarks>
    public const double LineBaselineTolerance = 2.0;

    /// <summary>
    /// A gap this many times the line's typical word gap ends a run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sharing a baseline does not make two runs of text one line. A form's
    /// header puts "Form W-9" at the left margin and "Give form to the" in a box
    /// at the right, and grouping by baseline alone joins them into a sentence
    /// nobody wrote — which then reads as a label for whatever field is nearby.
    /// </para>
    /// <para>
    /// <b>"Typical" is the lower quartile of the line's gaps, not the median.</b>
    /// That is a correction, and the second time this rule has been wrong. A
    /// median assumes most gaps on a line are word gaps, which fails exactly
    /// where splitting matters most: W-9's tax-classification row is five
    /// captions separated by tick boxes, so of its seven gaps four are ~23pt
    /// column breaks and three are ~2pt word spaces. The median is then 23pt,
    /// the threshold 69pt, and the whole row survives as one run — handing five
    /// checkboxes' captions to whichever one claims it first.
    /// </para>
    /// <para>
    /// The lower quartile is the fix because the contamination is always in the
    /// upper tail: column breaks are the <em>large</em> gaps, so a statistic
    /// taken from the small end estimates word spacing whatever the mix.
    /// </para>
    /// <para>
    /// Glyph height was tried as the yardstick instead and rejected as the less
    /// conservative of the two: it splits at a fixed fraction of the type size
    /// and so cannot see stretched word spacing, which is real on this corpus —
    /// <c>fed-w4</c> is justified. It produced 1018 lines against the quartile
    /// rule's 678 without a demonstrated benefit, so the quartile rule wins on
    /// caution rather than on a measured defect in the alternative.
    /// </para>
    /// <para>
    /// One consequence is worth stating because it looks alarming and is not.
    /// Splitting more finely drops <c>fed-w4</c>'s median line from 10 words to
    /// 3, which reads like shredding. It is dot leaders: the form rules its
    /// figures with runs of widely spaced periods, and the old threshold welded
    /// them to the label, yielding "2 Add lines 1a, 1b, and 1c. Enter the result
    /// here . . . . . . . 2 $" as a single line. Splitting that leaves a clean
    /// label and a row of dots that carry no letters, which page-furniture
    /// detection then discards. The line count rose because the extraction got
    /// better, not worse.
    /// </para>
    /// </remarks>
    public const double ColumnGapMultiple = 3.0;

    /// <summary>
    /// The smallest gap that may be treated as a column break, in points.
    /// </summary>
    /// <remarks>
    /// Guards the relative rule on a line whose words happen to sit unusually
    /// tight, where three times a tiny quartile would split ordinary spacing.
    /// </remarks>
    public const double MinimumColumnGap = 8.0;

    /// <summary>A gap this many times the glyph height is a column break whatever else is on the line.</summary>
    /// <remarks>
    /// The ceiling on the relative rule. Swept against the label oracles: 3
    /// and 4 score 212 and 213 agreed, 6 and 8 both score 215, and no ceiling
    /// at all scores 213. Six is where it settles — tight enough to break
    /// SS-4's one-word caption cells, loose enough not to cut into the
    /// stretched word spacing of justified text, which is what a smaller cap
    /// does to fed-ss4's precision (94% down to 89% at a cap of 3).
    /// </remarks>
    public const double AbsoluteBreakMultiple = 6.0;

    /// <summary>Serializer options that produce a stable, readable, diffable file.</summary>
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Extracts the reference from a PDF on disk.</summary>
    public static PdfStructuredReference Extract(string path, string formId, string? answerKey = null)
    {
        using var doc = PdfeDoc.Open(path);

        var pages = new List<ReferencePage>();
        var lines = new List<ReferenceLine>();

        for (var number = 1; number <= doc.Pages.Count; number++)
        {
            var page = doc.GetPage(number);
            pages.Add(new ReferencePage(number, Math.Round(page.Width, 2), Math.Round(page.Height, 2)));
            lines.AddRange(LinesOnPage(page, number));
        }

        // One order across the document: page, then down the page, then across.
        var ordered = lines
            .OrderBy(l => l.Page)
            .ThenByDescending(l => l.Rect.Top)
            .ThenBy(l => l.Rect.Left)
            .Select((l, i) => l with { Order = i })
            .ToList();

        var declared = PdfWidgetManifest.Extract(path).Importable
            .Where(e => e.Rect is not null && e.PageNumber is not null)
            .Select((e, declarationOrder) => (Entry: e, DeclarationOrder: declarationOrder))
            .ToList();

        var fields = declared
            .OrderBy(x => x.Entry.PageNumber!.Value)
            .ThenByDescending(x => x.Entry.Rect!.Value.Top)
            .ThenBy(x => x.Entry.Rect!.Value.Left)
            .Select((x, i) => new ReferenceField(
                i,
                x.DeclarationOrder,
                x.Entry.FullName,
                x.Entry.FieldType.ToString(),
                x.Entry.PageNumber!.Value,
                ReferenceRect.From(x.Entry.Rect!.Value),
                x.Entry.Tooltip,
                x.Entry.OptionCount))
            .ToList();

        return new PdfStructuredReference(formId, doc.Pages.Count, pages, fields, ordered, answerKey);
    }

    /// <summary>Serializes a reference to its committed JSON form.</summary>
    public static string ToJson(PdfStructuredReference reference) =>
        JsonSerializer.Serialize(reference, JsonOptions);

    private static IEnumerable<ReferenceLine> LinesOnPage(PdfPage page, int number)
    {
        var words = page.GetWords()
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .ToList();

        // Group by baseline, then read each line left to right. Order is assigned
        // document-wide by the caller, so 0 here is a placeholder.
        var groups = new List<List<Excise.Core.Text.Word>>();
        foreach (var word in words.OrderByDescending(w => w.BoundingBox.Bottom))
        {
            var group = groups.FirstOrDefault(g =>
                Math.Abs(g[0].BoundingBox.Bottom - word.BoundingBox.Bottom) <= LineBaselineTolerance);
            if (group is null)
            {
                groups.Add([word]);
            }
            else
            {
                group.Add(word);
            }
        }

        foreach (var group in groups)
        {
            foreach (var run in SplitAtColumnGaps(group.OrderBy(w => w.BoundingBox.Left).ToList()))
            {
                var text = string.Join(" ", run.Select(w => w.Text.Trim()));
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                yield return new ReferenceLine(
                    number,
                    0,
                    text,
                    new ReferenceRect(
                        Math.Round(run.Min(w => w.BoundingBox.Left), 2),
                        Math.Round(run.Min(w => w.BoundingBox.Bottom), 2),
                        Math.Round(run.Max(w => w.BoundingBox.Right), 2),
                        Math.Round(run.Max(w => w.BoundingBox.Top), 2)));
            }
        }
    }

    /// <summary>Breaks one baseline's words wherever the spacing says a column ended.</summary>
    private static List<List<Excise.Core.Text.Word>> SplitAtColumnGaps(List<Excise.Core.Text.Word> inOrder)
    {
        if (inOrder.Count < 2)
        {
            return [inOrder];
        }

        var gaps = new double[inOrder.Count - 1];
        for (var i = 1; i < inOrder.Count; i++)
        {
            gaps[i - 1] = inOrder[i].BoundingBox.Left - inOrder[i - 1].BoundingBox.Right;
        }

        // Lower quartile, not median: column breaks are the large gaps, so a
        // statistic taken from the small end still estimates word spacing on a
        // line that is mostly column breaks. See ColumnGapMultiple.
        var sorted = gaps.Where(g => g > 0).Order().ToArray();
        var typical = sorted.Length == 0 ? 0 : sorted[sorted.Length / 4];

        // Capped at a few times the line's own glyph height, because the
        // quartile has nothing to work with when EVERY gap on the line is a
        // column break. SS-4's line 13 is three one-word captions --
        // "Agricultural", "Household", "Other" -- in three ruled cells, so both
        // its gaps are ~50pt, the quartile is 50, the threshold becomes 150 and
        // the row survives as one run. A 50pt gap in 8pt type is a column break
        // whatever the rest of the line looks like.
        var height = inOrder.Max(w => w.BoundingBox.Height);
        var threshold = Math.Min(
            Math.Max(MinimumColumnGap, typical * ColumnGapMultiple),
            Math.Max(MinimumColumnGap, height * AbsoluteBreakMultiple));

        var runs = new List<List<Excise.Core.Text.Word>>();
        runs.Add([inOrder[0]]);
        for (var i = 1; i < inOrder.Count; i++)
        {
            if (gaps[i - 1] > threshold)
            {
                runs.Add([]);
            }

            runs[^1].Add(inOrder[i]);
        }

        return runs;
    }
}
