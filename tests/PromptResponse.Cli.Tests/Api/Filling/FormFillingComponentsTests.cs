using AwesomeAssertions;
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
        document.Sections[0].Sections[0].Prompts[0].ResponseMetadata.LastModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
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
    public void FilledFormWriter_EnsuresFilledFormExtension(string outputPath, string expectedPath) =>
        FilledFormWriter.EnsureFilledFormExtension(outputPath).Should().Be(expectedPath);

    [Fact]
    public void FilledFormFactory_ClonesTemplateBeforeAddingFilledFormMetadata()
    {
        var template = TestDocumentFactory.CreateMinimalTemplate();
        var factory = new FilledFormFactory(new AprJsonSerializer());

        var filled = factory.Create(template, "Test User");

        filled.Should().NotBeSameAs(template);
        filled.DocumentType.Should().Be(DocumentType.FilledForm);
        filled.Metadata.FilledBy.Should().Be("Test User");
        template.DocumentType.Should().Be(DocumentType.Template);
        template.Metadata.FilledBy.Should().BeNull();
    }
}
