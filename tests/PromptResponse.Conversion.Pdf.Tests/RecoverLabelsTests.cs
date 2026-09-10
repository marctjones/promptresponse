using AwesomeAssertions;
using PromptResponse.Conversion.Pdf;
using Xunit;

namespace PromptResponse.Conversion.Pdf.Tests;

/// <summary>
/// Two thirds of the corpus's fields arrive with a name like <c>f1_01[0]</c> and
/// no tooltip. The form prints a real question beside every one of them; this is
/// the phase that goes and gets it.
/// </summary>
public class RecoverLabelsTests
{
    [Fact]
    public void MostOfW9sCrypticFieldsGetARealLabel()
    {
        // W-9 is the hard, common case: all 23 fields are XFA names, and every
        // one of them has its question printed on the page.
        var result = ConversionPipeline.Default().Convert(CorpusPath("fed-w9"), "fed-w9");
        var fields = result.State.FieldsOrEmpty;

        fields.Should().HaveCount(23);
        fields.Count(f => f.HasLabel).Should().BeGreaterThanOrEqualTo(18,
            "the questions are printed beside the fields; failing to find most of them " +
            $"means the pairing is not working. Got: " +
            string.Join(" | ", fields.Select(f => f.Label ?? "(none)")));
    }

    [Fact]
    public void TheFirstFieldGetsTheQuestionPrintedAboveIt()
    {
        // Named specifically rather than left to a count, because a count can be
        // satisfied by 18 wrong answers.
        var result = ConversionPipeline.Default().Convert(CorpusPath("fed-w9"), "fed-w9");
        var first = result.State.FieldsOrEmpty
            .Where(f => f.PageNumber == 1)
            .OrderByDescending(f => f.TargetRect!.Value.Top)
            .First();

        first.Label.Should().NotBeNull();
        first.Label.Should().Contain("Name of entity",
            $"W-9's first field asks for the entity name. Got: '{first.Label}'");
    }

    [Fact]
    public void ACheckboxTakesTheLabelPrintedToItsRight()
    {
        // The rule a single "nearest text" heuristic gets wrong. W-9's seven tax
        // classification boxes each have their name to the right; the text above
        // them is one shared instruction, and letting all seven claim it is how
        // an instruction becomes seven field labels.
        var result = ConversionPipeline.Default().Convert(CorpusPath("fed-w9"), "fed-w9");

        var labels = result.State.FieldsOrEmpty
            .Where(f => f.ExpectedDataType == "boolean" && f.HasLabel)
            .Select(f => f.Label!)
            .ToList();

        labels.Should().Contain(l => l.Contains("corporation", StringComparison.OrdinalIgnoreCase),
            $"one of the checkboxes is 'C corporation'. Got: {string.Join(" | ", labels)}");
        labels.Should().NotContain(l => l.Contains("Check the appropriate box", StringComparison.OrdinalIgnoreCase),
            "that line is the instruction for the whole group, not any one box's label");
    }

    [Fact]
    public void OneSpanIsNotUsedAsTheLabelForManyFields()
    {
        // The competition rule. A line claimed by several scattered fields is a
        // group instruction, not a label, and handing it to all of them
        // manufactures duplicate questions that read as plausible.
        //
        // Sharing is not banned outright, and this test used to ban it. W-9
        // writes a social security number as three boxes split by the printed
        // dashes under one caption, so "Social security number" is the right
        // label for all three. What must not happen is the same line landing on
        // fields that are not one entry, so the check is that every shared
        // label belongs to a detected group.
        var result = ConversionPipeline.Default().Convert(CorpusPath("fed-w9"), "fed-w9");
        var fields = result.State.FieldsOrEmpty;

        var grouped = RepeatingRows.Detect(fields)
            .Concat(RepeatingRows.DetectRows(fields))
            .SelectMany(c => c.FieldIds)
            .ToHashSet(StringComparer.Ordinal);

        var loose = fields
            .Where(f => f.HasLabel)
            .GroupBy(f => f.Label!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 2 && g.Any(f => !grouped.Contains(f.Id)))
            .Select(g => $"'{g.Key}' x{g.Count()}")
            .ToList();

        loose.Should().BeEmpty(
            "a line may label several fields only when they are one entry the form split up; " +
            $"these are shared by fields that are not: {string.Join(", ", loose)}");
    }

    [Fact]
    public void TheRotatedSidebarIsNeverUsedAsALabel()
    {
        // W-9 prints "Print or type. See Specific Instructions on page 3" rotated
        // down the left margin. Read as horizontal runs it fragments into
        // "rint Instructions", "page", "type." -- all of which sit to the left of
        // real fields and would win a naive nearest-text contest.
        var result = ConversionPipeline.Default().Convert(CorpusPath("fed-w9"), "fed-w9");

        var labels = result.State.FieldsOrEmpty.Where(f => f.HasLabel).Select(f => f.Label!).ToList();
        var fragments = new[] { "page", "type.", "3." };
        labels.Should().NotContain(l => l.Contains("rint Instruction", StringComparison.Ordinal));
        labels.Should().NotContain(l => fragments.Contains(l.Trim()));
    }

    private static string CorpusPath(string id) =>
        Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "corpus", $"{id}.pdf");

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
}
