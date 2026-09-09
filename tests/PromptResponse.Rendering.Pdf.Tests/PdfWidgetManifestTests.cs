using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using Xunit;

namespace PromptResponse.Rendering.Pdf.Tests;

/// <summary>
/// The PDF's own field list is the one piece of reference data in this project that
/// needs neither a human nor a model: "what fields does this PDF declare" is answered
/// definitionally by the PDF. That makes it the right oracle for the one question it
/// can answer — did a conversion lose a field somebody is expected to fill in.
/// </summary>
public class PdfWidgetManifestTests
{
    private static AprDocument SourceForm() => new()
    {
        DocumentType = DocumentType.Template,
        Metadata = new Metadata { Title = "Manifest" },
        Sections =
        [
            new Section
            {
                Id = "s1", Title = "Details",
                Prompts =
                [
                    new Prompt { Id = "full_name", Label = "Full Name", Hints = new PromptHints { ExpectedDataType = "text" } },
                    new Prompt { Id = "agree", Label = "I agree", Hints = new PromptHints { ExpectedDataType = "boolean" } },
                    new Prompt { Id = "colour", Label = "Colour", Hints = new PromptHints { SuggestedValues = ["Red", "Green"] } },
                ],
            },
        ],
    };

    private static byte[] FillablePdf() => new FillablePdfDocumentRenderer().RenderToBytes(SourceForm());

    [Fact]
    public void TheManifestListsEveryFieldThePdfDeclares()
    {
        var manifest = PdfWidgetManifest.Extract(FillablePdf());

        manifest.Importable.Should().HaveCount(3, "three prompts were rendered as three widgets");
        manifest.Importable.Select(e => e.FullName).Should().BeEquivalentTo(["full_name", "agree", "colour"]);
        manifest.Importable.Should().AllSatisfy(e => e.FullName.Should().NotBeNullOrWhiteSpace(
            "the field name is the round-trip identity; a blank one would break it"));
    }

    [Fact]
    public void ImportingItsOwnSourcePdfLosesNothing()
    {
        var pdf = FillablePdf();
        var manifest = PdfWidgetManifest.Extract(pdf);

        var imported = new PdfFormImporter().Import(pdf, "Manifest");
        var coverage = PdfWidgetManifest.Compare(manifest, imported);

        coverage.IsComplete.Should().BeTrue(
            $"the importer must account for every declared field; missing: {string.Join(", ", coverage.MissingFieldNames)}");
        coverage.Fraction.Should().Be(1.0);
        coverage.UnaccountedPromptIds.Should().BeEmpty(
            "a pure AcroForm import should invent nothing that the PDF did not declare");
    }

    [Fact]
    public void ADroppedFieldIsReportedByName_NotJustCounted()
    {
        var manifest = PdfWidgetManifest.Extract(FillablePdf());

        // A conversion that quietly lost one field.
        var truncated = new AprDocument
        {
            DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "Manifest" },
            Sections = [new Section { Id = "s", Title = "S", Prompts = [new Prompt { Id = "full_name", Label = "Full Name" }] }],
        };

        var coverage = PdfWidgetManifest.Compare(manifest, truncated);

        coverage.IsComplete.Should().BeFalse();
        coverage.Covered.Should().Be(1);
        coverage.Expected.Should().Be(3);
        coverage.MissingFieldNames.Should().BeEquivalentTo(["agree", "colour"],
            "naming what went missing is the point — a bare fraction does not tell you what to go look at");
    }

    [Fact]
    public void ARepeatedFieldNameIsCountedTwice_NotCollapsed()
    {
        // The importer appends #2 to make a repeated field name unique as an APR id.
        // Comparing as sets rather than multisets would let a real loss hide behind
        // the surviving duplicate, so a manifest with two same-named fields needs two
        // prompts to be satisfied.
        var manifest = new WidgetManifest(
        [
            new WidgetManifestEntry("dupe", Excise.Core.Document.PdfFieldType.Text, 1, HasTooltip: false, OptionCount: 0),
            new WidgetManifestEntry("dupe", Excise.Core.Document.PdfFieldType.Text, 1, HasTooltip: false, OptionCount: 0),
        ]);

        var onlyOne = new AprDocument
        {
            DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "Dupes" },
            Sections = [new Section { Id = "s", Title = "S", Prompts = [new Prompt { Id = "dupe", Label = "Dupe" }] }],
        };
        PdfWidgetManifest.Compare(manifest, onlyOne).MissingFieldNames.Should().ContainSingle()
            .Which.Should().Be("dupe");

        var both = new AprDocument
        {
            DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "Dupes" },
            Sections =
            [
                new Section
                {
                    Id = "s", Title = "S",
                    Prompts = [new Prompt { Id = "dupe", Label = "Dupe" }, new Prompt { Id = "dupe#2", Label = "Dupe" }],
                },
            ],
        };
        PdfWidgetManifest.Compare(manifest, both).IsComplete.Should().BeTrue(
            "the #2 suffix is the importer's disambiguator, not a different field");
    }

    [Fact]
    public void TheOracleCoversTheWholeScoredCorpus_ForFree()
    {
        // Every form the model benchmark scored carries an AcroForm, so field
        // completeness is checkable across all of them with no answer key at all.
        // If this ever stops holding, the claim in the benchmark README is stale.
        var corpus = Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "corpus");
        Directory.Exists(corpus).Should().BeTrue();

        var benchmarkForms = new[]
        {
            "fed-w9", "fed-w4", "fed-ss4", "fed-8822", "fed-i9",
            "ct-w4", "ct-dmv-j23", "ct-dmv-b58ind", "ct-dmv-a25", "ct-dmv-a83", "ct-dmv-b225p",
        };

        foreach (var id in benchmarkForms)
        {
            var path = Path.Combine(corpus, $"{id}.pdf");
            File.Exists(path).Should().BeTrue($"{id} is a committed corpus form");
            PdfWidgetManifest.Extract(path).Importable.Should().NotBeEmpty(
                $"{id} carries an AcroForm, which is what makes the mechanical oracle free here");
        }
    }

    [Fact]
    public void ADerivedFlatFormInheritsItsSourcesFieldListAsAnAnswerKey()
    {
        // The reason the derived fixtures are worth more than the non-fillable
        // forms found in the wild. `fed-w9-flat` is what a converter must handle:
        // a printed federal form with no fields of its own. But it was made by
        // flattening `fed-w9`, so `fed-w9`'s AcroForm states exactly which fields
        // a correct conversion should recover -- names, types, options, and widget
        // geometry -- with no human and no model in the loop.
        //
        // This is the only route to mechanical ground truth for the converter's
        // actual target case, and `derivedFrom` in corpus_manifest.json is the
        // link that makes it available.
        var flat = PdfWidgetManifest.Extract(CorpusPath("fed-w9-flat"));
        var oracle = PdfWidgetManifest.Extract(CorpusPath("fed-w9"));

        flat.Importable.Should().BeEmpty("the flattened form declares no fields of its own");
        oracle.Importable.Should().NotBeEmpty(
            "its source does, and that list is the answer key for converting the flat one");

        // Guard the link itself: if flattening ever started preserving widgets,
        // or the two files drifted apart, the answer key would stop describing
        // the fixture it is supposed to grade.
        PdfSourceDetector.Detect(CorpusPath("fed-w9-flat")).Pages.Count
            .Should().Be(PdfSourceDetector.Detect(CorpusPath("fed-w9")).Pages.Count,
                "an answer key only applies if it describes the same pages");
    }

    [Fact]
    public void ANonFillableFormHasNoMechanicalOracle()
    {
        // The converter-development half of the corpus. Being honest about this is
        // the point: for the converter's actual target case, "did we find every
        // field" has no free answer and needs real reference data.
        var path = CorpusPath("bloomfield-citizen-complaint");
        File.Exists(path).Should().BeTrue();

        var manifest = PdfWidgetManifest.Extract(path);

        manifest.Importable.Should().BeEmpty("a print-and-fill municipal form declares no fields");
        PdfWidgetManifest.Compare(manifest, SourceForm()).Fraction.Should().Be(1.0,
            "with nothing declared, coverage is vacuously complete — which is exactly why " +
            "this oracle cannot grade the converter's target case, and must not be read as " +
            "evidence that it did well");
    }

    private static string CorpusPath(string id) =>
        Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "corpus", $"{id}.pdf");

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
}
