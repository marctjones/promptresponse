using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using PromptResponse.Core.Validation;
using Xunit;

namespace PromptResponse.Rendering.Pdf.Tests;

/// <summary>
/// Verifies the PDF AcroForm importer. Uses a round-trip: render a known APR to a
/// fillable PDF (which names each field by id and sets its tooltip to the label),
/// then import it back and assert the fields, labels, and types survived. Also
/// checks the flat-PDF (no AcroForm) path and that the result validates.
/// </summary>
public class PdfFormImporterTests
{
    private static AprDocument SourceForm() => new()
    {
        DocumentType = DocumentType.Template,
        Metadata = new Metadata { Title = "Sign-up" },
        Sections =
        [
            new Section
            {
                Id = "s1", Title = "Details",
                Prompts =
                [
                    new Prompt { Id = "full_name", Label = "Full Name", Hints = new PromptHints { ExpectedDataType = "text" } },
                    new Prompt { Id = "agree", Label = "I agree", Hints = new PromptHints { ExpectedDataType = "boolean" } },
                    new Prompt { Id = "color", Label = "Colour", Hints = new PromptHints { SuggestedValues = ["Red", "Green"] } },
                ],
            },
        ],
    };

    [Fact]
    public void Import_FillablePdf_RecoversFieldsAsPrompts()
    {
        var pdfBytes = new FillablePdfDocumentRenderer().RenderToBytes(SourceForm());

        var imported = new PdfFormImporter().Import(pdfBytes, "Sign-up");

        var prompts = imported.Sections.SelectMany(s => s.Prompts).ToList();
        prompts.Should().HaveCountGreaterThanOrEqualTo(3);

        // The fillable renderer sets /TU (tooltip) = the visible label, so the
        // importer recovers the human label rather than the raw field name.
        prompts.Select(p => p.Label).Should().Contain(["Full Name", "I agree", "Colour"]);

        prompts.Single(p => p.Label == "Full Name").Hints.ExpectedDataType.Should().Be("text");
        prompts.Single(p => p.Label == "I agree").Hints.ExpectedDataType.Should().Be("boolean");
        var colour = prompts.Single(p => p.Label == "Colour");
        colour.Hints.SuggestedValues.Should().Contain(["Red", "Green"]);
    }

    [Fact]
    public void Import_ProducesValidTemplate()
    {
        var pdfBytes = new FillablePdfDocumentRenderer().RenderToBytes(SourceForm());

        var imported = new PdfFormImporter().Import(pdfBytes, "Sign-up");

        imported.DocumentType.Should().Be(DocumentType.Template);
        imported.Sections.Should().NotBeEmpty();
        new DocumentValidator().Validate(imported).IsValid
            .Should().BeTrue("an imported form must be a structurally valid template");
    }

    [Fact]
    public void Import_AllResponsesAreBlank_ItIsATemplate()
    {
        var pdfBytes = new FillablePdfDocumentRenderer().RenderToBytes(SourceForm());

        var imported = new PdfFormImporter().Import(pdfBytes, "Sign-up");

        imported.Sections.SelectMany(s => s.Prompts).Select(p => p.Response)
            .Should().OnlyContain(r => r == string.Empty);
    }

    [Fact]
    public void ImportWithQuality_HumanLabels_ScoresHigh_UseDirectly()
    {
        var pdfBytes = new FillablePdfDocumentRenderer().RenderToBytes(SourceForm());

        var (_, quality) = new PdfFormImporter().ImportWithQuality(pdfBytes, "Sign-up");

        quality.Score.Should().BeGreaterThanOrEqualTo(70);
        quality.Recommendation.Should().Be(ImportRecommendation.UseDirectly);
        quality.CrypticLabelRatio.Should().Be(0);
        quality.Flags.Should().NotContain(f => f.Kind == FieldFlagKind.CrypticLabel);
    }

    [Fact]
    public void ImportWithQuality_CrypticLabels_ScoresLow_RecommendsSkill_WithFlags()
    {
        // A form whose labels are raw field names (what a tooltip-less PDF yields).
        var doc = new AprDocument
        {
            DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "Raw" },
            Sections =
            [
                new Section { Id = "p1", Title = "Page 1", Prompts =
                [
                    new Prompt { Id = "f1_1", Label = "f1_1[0]", Hints = new PromptHints { ExpectedDataType = "text" } },
                    new Prompt { Id = "f1_2", Label = "f1_2[0]", Hints = new PromptHints { ExpectedDataType = "text" } },
                    new Prompt { Id = "c1_1", Label = "c1_1[0]", Hints = new PromptHints { ExpectedDataType = "boolean" } },
                ]},
            ],
        };
        var pdfBytes = new FillablePdfDocumentRenderer().RenderToBytes(doc);

        var (_, quality) = new PdfFormImporter().ImportWithQuality(pdfBytes, "Raw");

        quality.Score.Should().BeLessThan(40);
        quality.Grade.Should().Be("F");
        quality.Recommendation.Should().Be(ImportRecommendation.UseSkillInstead);
        quality.CrypticLabelRatio.Should().BeGreaterThan(0.9);
        quality.Flags.Should().Contain(f => f.Kind == FieldFlagKind.CrypticLabel);
        quality.Summary.Should().Contain("document-to-apr skill");
    }

    [Fact]
    public void Import_FlatPdf_ThrowsNoFormFields()
    {
        // A flat (non-fillable) PDF has no AcroForm to import.
        var flatBytes = new PdfDocumentRenderer().RenderToBytes(SourceForm());

        var act = () => new PdfFormImporter().Import(flatBytes, "Sign-up");

        act.Should().Throw<PdfFormImporter.NoFormFieldsException>();
    }

    [Fact]
    public void Import_SkipsPushButtons_BecauseTheyCarryNoAnswer()
    {
        // A push button is a JavaScript action -- "Print Form", "Clear Fields" --
        // not a field anybody fills in. Importing one produces a yes/no question
        // that cannot be answered and that nothing on the real form asks.
        //
        // CT-W4 is the corpus fixture that has them: two push buttons among 21
        // AcroForm fields. They are also the only reason its flattened twin loses
        // any words at all, since flattening correctly drops the buttons.
        var path = Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "corpus", "ct-w4.pdf");
        File.Exists(path).Should().BeTrue();

        var imported = new PdfFormImporter().Import(path, "CT-W4");
        var prompts = imported.Sections.SelectMany(s => s.Prompts).ToList();

        prompts.Select(p => p.Id).Should().NotContain(["Print Form", "Clear Fields"],
            "a push button is an action, not a question");
        prompts.Should().HaveCount(19, "21 AcroForm fields minus the 2 push buttons");
    }

    [Fact]
    public void TheWidgetOracleExcludesPushButtonsToo()
    {
        // Otherwise the mechanical oracle would expect the importer to account for
        // fields it is right to skip, and report a correct import as incomplete.
        var path = Path.Combine(RepoRoot, "scripts", "pdf-form-benchmark", "corpus", "ct-w4.pdf");

        var manifest = PdfWidgetManifest.Extract(path);
        manifest.Importable.Select(e => e.FullName).Should().NotContain(["Print Form", "Clear Fields"]);

        var coverage = PdfWidgetManifest.Compare(manifest, new PdfFormImporter().Import(path, "CT-W4"));
        coverage.IsComplete.Should().BeTrue(
            $"missing: {string.Join(", ", coverage.MissingFieldNames)}");
        coverage.UnaccountedPromptIds.Should().BeEmpty();
    }

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));

    [Fact]
    public void QualityAssessor_DuplicateReadableLabels_AppliesPenaltyAndFlagsEveryPrompt()
    {
        var mappings = new[]
        {
            Mapping("first", "Email address", hadTooltip: true),
            Mapping("second", "Email address", hadTooltip: true),
            Mapping("third", "Phone number", hadTooltip: true),
        };

        var quality = PdfImportQualityAssessor.Assess(mappings);

        quality.Score.Should().Be(85);
        quality.Recommendation.Should().Be(ImportRecommendation.UseDirectly);
        quality.DuplicateLabelRatio.Should().BeApproximately(2d / 3, 0.0001);
        quality.Flags.Where(flag => flag.Kind == FieldFlagKind.DuplicateLabel)
            .Select(flag => flag.PromptId).Should().BeEquivalentTo(["first", "second"]);
    }

    [Fact]
    public void DocumentBuilder_GroupsMappedPromptsByPageAndCreatesStableMetadata()
    {
        var document = PdfImportDocumentBuilder.Build("  Tax form 2026! ",
        [
            Mapping("unknown", "Unpaged", pageNumber: 0),
            Mapping("page-two", "Second page", pageNumber: 2),
            Mapping("page-one", "First page", pageNumber: 1),
        ]);

        document.Metadata.TemplateId.Should().Be("tag:skpt.cl,2026:imported/tax-form-2026");
        document.Sections.Select(section => section.Id).Should().Equal("fields", "page-1", "page-2");
        document.Sections[2].Description.Should().Be("Fields from page 2 of the source PDF.");
        document.Sections[1].Prompts.Single().Id.Should().Be("page-one");
    }

    private static PdfImportFieldMapping Mapping(
        string id,
        string label,
        bool hadTooltip = false,
        int pageNumber = 1) => new(
            new Prompt { Id = id, Label = label, Hints = new PromptHints() },
            pageNumber,
            hadTooltip,
            IsButton: false,
            OptionCount: 0);
}
