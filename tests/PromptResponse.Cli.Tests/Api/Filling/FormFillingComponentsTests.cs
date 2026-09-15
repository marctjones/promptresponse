using AwesomeAssertions;
using PromptResponse.Cli;
using PromptResponse.Cli.Api.Filling;
using PromptResponse.Cli.Tests.Fixtures;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Cli.Tests.Api.Filling;

public class FormFillingComponentsTests
{
    [Fact]
    public void ResponseApplicator_AppliesNestedResponsesAndReportsUnknownIds()
    {
        var document = TestDocumentFactory.CreateComplexTemplate();

        var result = new FormResponseApplicator().Apply(document, new Dictionary<string, string>
        {
            ["prompt_002"] = "nested@example.com",
            ["missing"] = "ignored"
        });

        result.AppliedCount.Should().Be(1);
        result.MissingPromptIds.Should().ContainSingle().Which.Should().Be("missing");
        document.Sections[0].Sections[0].Prompts[0].Response.Should().Be("nested@example.com");
    }

    [Fact]
    public void ResponseApplicator_UsesFirstPromptWhenDocumentHasDuplicateIds()
    {
        var document = TestDocumentFactory.CreateComplexTemplate();
        document.Sections[1].Prompts[0].Id = "prompt_001";

        new FormResponseApplicator().Apply(document, new Dictionary<string, string> { ["prompt_001"] = "first" });

        document.Sections[0].Prompts[0].Response.Should().Be("first");
        document.Sections[1].Prompts[0].Response.Should().BeEmpty();
    }

    [Fact]
    public void PromptMetrics_IncludeNestedPromptsAndIgnoreWhitespaceResponses()
    {
        var document = TestDocumentFactory.CreateComplexTemplate();
        document.Sections[0].Prompts[0].Response = "answer";
        document.Sections[0].Sections[0].Prompts[0].Response = " ";

        FormPromptMetrics.GetPromptIds(document).Should().Equal("prompt_001", "prompt_002", "prompt_003", "prompt_004");
        FormPromptMetrics.GetCompletionPercentage(document).Should().Be(25);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"prompt_001\": \"answer\"}")]
    public void ResponseJsonParser_ParsesResponseObjects(string json)
    {
        FormResponseJsonParser.Parse(json).Should().NotBeNull();
    }

    [Fact]
    public void ResponseJsonParser_NullPayload_ReportsInvalidFormat()
    {
        Action act = () => FormResponseJsonParser.Parse("null");

        act
            .Should().Throw<InvalidOperationException>()
            .WithMessage("Invalid JSON format");
    }

    [Theory]
    [InlineData("filled", "filled.aprf")]
    [InlineData("filled.APRF", "filled.APRF")]
    [InlineData("filled.apr.yaml", "filled.apr.yaml")]
    [InlineData("filled.yaml", "filled.yaml")]
    [InlineData("filled.YML", "filled.YML")]
    public void FilledFormWriter_EnsuresFilledFormExtension(string outputPath, string expectedPath) =>
        FilledFormWriter.EnsureFilledFormExtension(outputPath).Should().Be(expectedPath);

    [Fact]
    public async Task FilledFormWriter_WritesYaml_WhenTheOutputPathSaysSo()
    {
        var document = TestDocumentFactory.CreateMinimalTemplate();
        var dir = Directory.CreateTempSubdirectory("filled-form-writer");
        try
        {
            var path = Path.Combine(dir.FullName, "answer.apr.yaml");

            var written = await new FilledFormWriter().WriteAsync(document, path);

            written.Should().Be(path);
            var text = await File.ReadAllTextAsync(path);
            text.Should().NotContain("{", "a YAML output must not be JSON");
            text.Should().Contain("aprVersion:");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void FilledFormFactory_ClonesTemplateBeforeAddingFilledFormMetadata()
    {
        var template = TestDocumentFactory.CreateMinimalTemplate();
        var factory = new FilledFormFactory(new AprJsonSerializer());

        var filled = factory.Create(template, "Test User");

        filled.Should().NotBeSameAs(template);
        filled.DocumentType.Should().Be(DocumentType.FilledForm);
        template.DocumentType.Should().Be(DocumentType.Template);
    }

    [Fact]
    public void FilledFormFactory_KeepsAnUnprefixedMemberTheTemplateCarried()
    {
        // `apr fill` clones through Beta6AprSerializer, whose write refuses an unprefixed
        // member set in code. One the template arrived with is not set in code, and a
        // writer puts it back (APR-MODEL-021) -- this used to throw before filling began.
        var serializer = new Beta6AprSerializer();
        var template = serializer.Deserialize(
            """{"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","tableLayout":{"fixedRows":2},"prompts":[{"id":"p","label":"P"}]}]}""");

        var filled = new FilledFormFactory(serializer).Create(template, null);

        filled.Sections[0].Extensions.Should().ContainKey("tableLayout");
        serializer.Serialize(filled).Should().Contain("tableLayout");
    }
}
