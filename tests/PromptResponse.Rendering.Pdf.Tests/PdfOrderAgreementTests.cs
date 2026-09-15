using System.Text.Json;
using AwesomeAssertions;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Rendering.Pdf.Tests;

/// <summary>
/// The hole every field-completeness check leaves open. Coverage is a multiset
/// comparison, so it is order-blind by construction: before this existed, a W-9
/// with every question asked backwards scored a perfect 1.00.
/// </summary>
public class PdfOrderAgreementTests
{
    [Fact]
    public void AFormAskedBackwardsIsNotCorrect()
    {
        // The demonstration that motivated this file. The same prompts, exactly
        // reversed -- nothing lost, nothing invented, and completely wrong for a
        // document a person fills in top to bottom.
        var reference = Load("fed-w9");
        var correct = new PdfFormImporter().Import(CorpusPath("fed-w9"), "fed-w9");

        var forwards = PdfOrderAgreement.Compare(reference, correct, useDeclarationOrder: true);
        var backwards = PdfOrderAgreement.Compare(reference, Reversed(correct), useDeclarationOrder: true);

        forwards.Tau.Should().Be(1.0, "the importer emits fields in declaration order");
        forwards.IsInOrder.Should().BeTrue();

        backwards.Tau.Should().Be(-1.0, "exact reversal is the worst possible ordering");
        backwards.IsInOrder.Should().BeFalse();
        backwards.FirstInversion.Should().NotBeNull(
            "a score says the form is jumbled; naming a pair says where to look");

        // And the point: coverage cannot tell these two apart.
        var manifest = PdfWidgetManifest.Extract(CorpusPath("fed-w9"));
        PdfWidgetManifest.Compare(manifest, Reversed(correct)).Fraction.Should().Be(1.0,
            "which is exactly why order needs its own measurement");
    }

    [Fact]
    public void ReversalNegatesTheScore_WhicheverOrderingIsTheReference()
    {
        // True by construction for a rank correlation, and worth pinning because
        // it is what makes the number comparable across forms: a form is not
        // "0.5 wrong" on one reference and "0.9 wrong" on another.
        var reference = Load("fed-w9");
        var document = new PdfFormImporter().Import(CorpusPath("fed-w9"), "fed-w9");

        foreach (var declaration in new[] { true, false })
        {
            var forwards = PdfOrderAgreement.Compare(reference, document, declaration);
            var backwards = PdfOrderAgreement.Compare(reference, Reversed(document), declaration);
            backwards.Tau.Should().BeApproximately(-forwards.Tau, 1e-9);
        }
    }

    [Fact]
    public void OneSwappedPairScoresNearlyRight_NotEquallyWrong()
    {
        // A rank correlation rather than a pass/fail, because two adjacent
        // questions swapped is a small defect and a reversed form is a different
        // kind of failure. A metric that scored them alike would hide that.
        var reference = Load("fed-w9");
        var document = new PdfFormImporter().Import(CorpusPath("fed-w9"), "fed-w9");

        var prompts = document.Sections.SelectMany(s => s.Prompts).ToList();
        (prompts[0], prompts[1]) = (prompts[1], prompts[0]);

        var clean = PdfOrderAgreement.Compare(reference, document, useDeclarationOrder: true);
        var swapped = PdfOrderAgreement.Compare(reference, Rebuild(document, prompts), useDeclarationOrder: true);

        clean.Discordant.Should().Be(0);
        swapped.Discordant.Should().Be(1, "exactly one pair changed places");
        swapped.Tau.Should().BeGreaterThan(0.9, "one swap out of 23 fields is a small defect");
        swapped.Tau.Should().BeLessThan(1.0);
    }

    [Fact]
    public void PromptsWithNoReferenceFieldDoNotSilentlyCountAsOrdered()
    {
        // A converter that invented fields would otherwise get them scored as
        // correctly placed, which is the flattering reading of its own mistake.
        var reference = Load("fed-w9");
        var document = new PdfFormImporter().Import(CorpusPath("fed-w9"), "fed-w9");

        var prompts = document.Sections.SelectMany(s => s.Prompts).ToList();
        prompts.Insert(0, new Prompt { Id = "invented-field", Label = "Where did this come from?" });

        var agreement = PdfOrderAgreement.Compare(reference, Rebuild(document, prompts), useDeclarationOrder: true);

        agreement.UnmatchedPromptIds.Should().ContainSingle().Which.Should().Be("invented-field");
        agreement.Comparable.Should().Be(23, "the invented prompt takes no part in the ordering score");
        agreement.IsInOrder.Should().BeTrue("the real fields are still in the right order");
    }

    [Fact]
    public void DeclarationOrderAndReadingOrderDisagree_SoBothAreRecorded()
    {
        // Measured, not assumed. Across the corpus the AcroForm's own field order
        // and geometric reading order agree with a Kendall tau of 0.55 to 1.00:
        // fed-8822 and ct-dmv-a83 match exactly, while ct-dmv-j23 (0.55) declares
        // CheckBox12 before CheckBox4 and fed-i9 (0.76) is a dense multi-column
        // form where a naive down-then-across sort is a poor model of reading.
        //
        // Neither is authoritative alone -- W-9 names one of its groups
        // "Boxes3a-b_ReadOrder", so the IRS clearly encoded intent in declaration
        // order, yet a converter reading a flattened form has no access to it.
        // That is why the reference carries both, and why a low geometric score
        // is a prompt to look rather than proof of a bug.
        var disagreements = new List<(string Id, double Tau)>();
        foreach (var id in new[] { "fed-w9", "fed-8822", "fed-i9", "ct-dmv-j23", "ct-dmv-a83" })
        {
            var geometric = PdfOrderAgreement.Compare(
                Load(id), new PdfFormImporter().Import(CorpusPath(id), id));
            disagreements.Add((id, geometric.Tau));
        }

        var summary = string.Join(" | ", disagreements.Select(d => $"{d.Id}:{d.Tau:F2}"));

        disagreements.Should().Contain(d => d.Tau < 0.99,
            $"at least one form's declaration order differs from reading order -- {summary}");
        disagreements.Should().AllSatisfy(d => d.Tau.Should().BeGreaterThan(0.4,
            $"but they broadly track each other on real forms -- {summary}"));
        disagreements.Should().Contain(d => d.Tau >= 0.999,
            $"and on a simple single-column form they agree exactly -- {summary}");
    }

    private static AprDocument Reversed(AprDocument document) => new()
    {
        DocumentType = document.DocumentType,
        Metadata = document.Metadata,
        Sections = [.. document.Sections.Select(s => new Section
        {
            Id = s.Id, Title = s.Title, Prompts = [.. s.Prompts.AsEnumerable().Reverse()],
        })],
    };

    private static AprDocument Rebuild(AprDocument document, List<Prompt> prompts) => new()
    {
        DocumentType = document.DocumentType,
        Metadata = document.Metadata,
        Sections = [new Section { Id = "s", Title = "All", Prompts = prompts }],
    };

    private static PdfStructuredReference Load(string id) =>
        JsonSerializer.Deserialize<PdfStructuredReference>(
            File.ReadAllText(Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "reference", $"{id}.json")),
            PdfReferenceExtractor.JsonOptions)!;

    private static string CorpusPath(string id) =>
        Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "corpus", $"{id}.pdf");

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
}
