using AwesomeAssertions;
using Xunit;
using PdfeDoc = Excise.Core.Document.PdfDocument;

namespace PromptResponse.Rendering.Pdf.Tests;

/// <summary>
/// The two things every discovery source must report about a field it found:
/// where it came from, and where on the page the answer goes.
/// </summary>
/// <remarks>
/// Neither reaches the <c>.aprt</c> — APR is layout-free and records no
/// provenance — so nothing downstream of assembly would notice if they stopped
/// being populated. They are carried for the two consumers that cannot recover
/// them later: the label↔input pairing shared by #426 and #424, and the
/// geometry-matched oracle that grades a converted flat form against its source
/// PDF's widget rects. That makes them exactly the kind of field that rots
/// silently, so it is asserted here against real forms rather than trusted.
/// </remarks>
public class PdfFieldOriginTests
{
    [Theory]
    [InlineData("fed-w9", 23)]
    [InlineData("ct-dmv-j23", 44)]
    [InlineData("fed-i9", 128)]
    public void EveryAcroFormFieldIsPlacedAndAttributed(string id, int expected)
    {
        var mappings = MapCorpusForm(id);

        mappings.Should().HaveCount(expected);
        mappings.Should().AllSatisfy(m =>
            m.Origin.Should().Be(PdfFieldOrigin.AcroFormWidget,
                "these came from real widgets, and a metric that cannot say which phase " +
                "produced a field cannot attribute a regression to one"));

        mappings.Should().AllSatisfy(m => m.TargetRect.Should().NotBeNull(
            "a widget states its own /Rect; without it the geometry oracle has nothing " +
            "to match against and the derived fixtures' answer keys stay unusable"));
    }

    [Fact]
    public void ThePlacementIsTheWidgetsOwnRectangle_NotAPlaceholder()
    {
        // A rect that is present but degenerate would satisfy a null check while
        // being useless for pairing or IoU matching.
        var mappings = MapCorpusForm("fed-w9");

        mappings.Should().AllSatisfy(m =>
        {
            var rect = m.TargetRect!.Value;
            rect.Width.Should().BePositive("a zero-width target cannot be paired or scored");
            rect.Height.Should().BePositive();
            rect.Right.Should().BeGreaterThan(rect.Left);
            rect.Top.Should().BeGreaterThan(rect.Bottom, "PDF user space has y increasing upward");
        });

        // And it must be the real geometry: W-9 is US Letter, so every field sits
        // inside 612x792 points. A transform bug or a unit mix-up lands outside.
        mappings.Should().AllSatisfy(m =>
        {
            var rect = m.TargetRect!.Value;
            rect.Left.Should().BeInRange(0, 612);
            rect.Right.Should().BeInRange(0, 612);
            rect.Bottom.Should().BeInRange(0, 792);
            rect.Top.Should().BeInRange(0, 792);
        });
    }

    private static List<PdfImportFieldMapping> MapCorpusForm(string id)
    {
        var path = Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "corpus", $"{id}.pdf");
        File.Exists(path).Should().BeTrue($"{id} is a committed corpus form");

        using var doc = PdfeDoc.Open(path);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordinal = 0;

        return [.. (doc.GetAcroForm()?.Fields ?? [])
            .Where(PdfImportableField.CarriesAnAnswer)
            .Select(f => PdfImportFieldMapper.Map(f, ++ordinal, seen))];
    }

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
}
