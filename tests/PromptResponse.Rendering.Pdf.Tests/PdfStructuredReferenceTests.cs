using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace PromptResponse.Rendering.Pdf.Tests;

/// <summary>
/// APR is not a visual format, so a conversion is checked structurally: are all
/// the fields here, which are missing, which text belongs to which field, and are
/// the questions asked in the right order. That needs the PDF's content as data,
/// which is what these reference files are.
/// </summary>
public class PdfStructuredReferenceTests
{
    private static readonly string[] Forms =
    [
        "fed-w9", "fed-w4", "fed-ss4", "fed-8822", "fed-i9",
        "ct-w4", "ct-dmv-j23", "ct-dmv-b58ind", "ct-dmv-a25", "ct-dmv-a83", "ct-dmv-b225p",
    ];

    public static TheoryData<string> AllForms() => [.. Forms];

    [Theory]
    [MemberData(nameof(AllForms))]
    public void TheCommittedReferenceMatchesWhatThePdfStates(string id)
    {
        var actual = PdfReferenceExtractor.ToJson(
            PdfReferenceExtractor.Extract(CorpusPath(id), id));
        var file = Path.Combine(ReferenceDir, $"{id}.json");

        if (Environment.GetEnvironmentVariable("UPDATE_PDF_REFERENCE") == "1")
        {
            Directory.CreateDirectory(ReferenceDir);
            File.WriteAllText(file, actual);
            return;
        }

        File.Exists(file).Should().BeTrue(
            $"{id}'s reference is committed; regenerate with UPDATE_PDF_REFERENCE=1");
        File.ReadAllText(file).Should().Be(actual,
            "the reference is extracted, not authored -- a diff means the extractor changed " +
            "or the PDF did, and either way it should be reviewed rather than absorbed");
    }

    [Fact]
    public void EveryFieldIsPlacedSoItCanBeVerifiedLater()
    {
        // The point of carrying geometry: a person or a later process can find
        // the field on the page. A reference with no coordinates can say a field
        // is missing but never where to go look.
        var reference = Load("fed-w9");
        var page = reference.Pages.Single(p => p.Number == 1);

        reference.Fields.Should().NotBeEmpty();
        reference.Fields.Should().AllSatisfy(f =>
        {
            f.Rect.Right.Should().BeGreaterThan(f.Rect.Left);
            f.Rect.Top.Should().BeGreaterThan(f.Rect.Bottom);
            f.Page.Should().BeInRange(1, reference.PageCount);
        });

        reference.Fields.Where(f => f.Page == 1).Should().AllSatisfy(f =>
        {
            f.Rect.Left.Should().BeInRange(0, page.Width);
            f.Rect.Top.Should().BeInRange(0, page.Height);
        });
    }

    [Fact]
    public void EveryLineIsPlacedToo_SoTextCanBePairedWithAFieldByHand()
    {
        // The other half of "which text should be the prompt for which field".
        // The reference deliberately does NOT answer that pairing -- that is what
        // the converter is graded on, so deriving it here would grade the
        // converter against itself. It supplies the evidence, not the verdict.
        var reference = Load("fed-w9");

        reference.Lines.Should().NotBeEmpty();
        reference.Lines.Should().AllSatisfy(l =>
        {
            l.Text.Should().NotBeNullOrWhiteSpace();
            l.Rect.Right.Should().BeGreaterThan(l.Rect.Left);
        });

        // And the evidence is genuinely there: a known W-9 label is present, with
        // a box, on the page it belongs to.
        reference.Lines.Should().Contain(l => l.Text.Contains("Business name", StringComparison.OrdinalIgnoreCase),
            "a real form label must survive extraction, or the reference cannot support pairing at all");
    }

    [Fact]
    public void SideBySideColumnsAreNotJoinedIntoOneLine()
    {
        // Sharing a baseline does not make two runs of text one line. W-9's
        // header puts "Form W-9" at the left margin and "Give form to the" in a
        // box at the right; grouping by baseline alone produced
        // "Form W-9 Give form to the" -- a sentence nobody wrote, which would
        // then read as a label for whichever field sits nearby.
        var lines = Load("fed-w9").Lines;

        lines.Should().Contain(l => l.Text == "Form W-9");
        lines.Should().Contain(l => l.Text == "Give form to the");
        lines.Should().NotContain(l => l.Text.Contains("Form W-9 Give"),
            "those are two columns, and joining them invents text the form does not contain");

        // The two are on one baseline and separated horizontally -- which is
        // exactly the case the split exists for.
        var left = lines.First(l => l.Text == "Form W-9");
        var right = lines.First(l => l.Text == "Give form to the");
        right.Rect.Left.Should().BeGreaterThan(left.Rect.Right,
            "they are side by side, not stacked");
    }

    [Fact]
    public void OrderIsAContiguousSequence_NotJustAnArrayIndex()
    {
        // Order is recorded because a form asked backwards is wrong even when
        // every field is present. It is geometric (down the page, then across),
        // which is right for a single-column form and approximate for a
        // multi-column one -- so it is review material, not an unchecked oracle.
        var reference = Load("fed-w9");

        reference.Fields.Select(f => f.Order).Should().BeInAscendingOrder()
            .And.OnlyHaveUniqueItems();
        reference.Fields.Select(f => f.Order).Should().Equal(Enumerable.Range(0, reference.Fields.Count));

        reference.Lines.Select(l => l.Order).Should().Equal(Enumerable.Range(0, reference.Lines.Count));

        // Ordering runs down each page before moving to the next.
        reference.Fields.Select(f => f.Page).Should().BeInAscendingOrder();
    }

    [Fact]
    public void TheFormAuthorsOwnLabelIsKeptWhereItExists()
    {
        // /TU is the one label answer nobody inferred. Recording whether it was
        // present is not enough -- the text is the answer key.
        var i9 = Load("fed-i9");
        i9.LabelledFieldCount.Should().Be(i9.Fields.Count,
            "every I-9 field carries a /TU, which makes it the label oracle's best case");
        i9.Fields.Should().Contain(f => f.Label != null && f.Label.Contains("Last Name", StringComparison.OrdinalIgnoreCase));

        // And the IRS forms are the opposite case, which is why #426 exists.
        Load("fed-w9").LabelledFieldCount.Should().Be(0,
            "W-9's fields are XFA-generated names with no /TU at all");
    }

    [Fact]
    public void TheReferenceCoversEveryFormAndNoOthers()
    {
        Directory.Exists(ReferenceDir).Should().BeTrue();
        Directory.GetFiles(ReferenceDir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(x => x, StringComparer.Ordinal)
            .Should().BeEquivalentTo(Forms.OrderBy(x => x, StringComparer.Ordinal));
    }

    private static PdfStructuredReference Load(string id) =>
        JsonSerializer.Deserialize<PdfStructuredReference>(
            File.ReadAllText(Path.Combine(ReferenceDir, $"{id}.json")),
            PdfReferenceExtractor.JsonOptions)!;

    private static string CorpusPath(string id) =>
        Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "corpus", $"{id}.pdf");

    private static string ReferenceDir =>
        Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "reference");

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
}
