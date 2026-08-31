using AwesomeAssertions;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Serialization;

public sealed class AprJsonSerializerSerializationTests : AprJsonSerializerTestBase
{
    [Fact]
    public void Serialize_WithSimpleDocument_ShouldProduceValidJson()
    {
        var json = Serializer.Serialize(new AprDocument { Version = AprFormat.CurrentVersion, DocumentType = DocumentType.Template, Metadata = new Metadata { Title = "Test Form" }, Sections = [new Section { Id = "section_001", Title = "Test Section" }] });
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("\"version\": \"1.0-beta.6\"");
        json.Should().Contain("\"documentType\": \"template\"");
        json.Should().Contain("\"title\": \"Test Form\"");
    }

    [Fact]
    public void Serialize_WithComplexDocument_ShouldIncludeAllStructure()
    {
        var json = Serializer.Serialize(CreateComplexDocument());
        json.Should().Contain("\"version\": \"1.0-beta.6\"");
        json.Should().Contain("\"documentType\": \"template\"");
        json.Should().Contain("\"sections\"");
        json.Should().Contain("\"prompts\"");
    }

    [Fact]
    public void RoundTrip_SimpleDocument_ShouldPreserveData()
    {
        var original = new AprDocument { Version = AprFormat.CurrentVersion, DocumentType = DocumentType.Template, Metadata = new Metadata { Title = "Test Form", Description = "A test" }, Sections = [new Section { Id = "section_001", Title = "Section 1", Prompts = [new Prompt { Id = "prompt_001", Label = "Question 1", Response = "Answer 1" }] }] };
        var deserialized = Serializer.Deserialize(Serializer.Serialize(original));
        deserialized.Version.Should().Be(original.Version);
        deserialized.DocumentType.Should().Be(original.DocumentType);
        deserialized.Metadata.Title.Should().Be(original.Metadata.Title);
        deserialized.Metadata.Description.Should().Be(original.Metadata.Description);
        deserialized.Sections.Should().HaveCount(1);
        deserialized.Sections[0].Id.Should().Be("section_001");
        deserialized.Sections[0].Prompts.Should().HaveCount(1);
        deserialized.Sections[0].Prompts[0].Response.Should().Be("Answer 1");
    }

    [Fact]
    public void RoundTrip_ComplexDocument_ShouldPreserveAllData()
    {
        var original = CreateComplexDocument();
        var deserialized = Serializer.Deserialize(Serializer.Serialize(original));
        deserialized.Sections.Should().HaveCount(original.Sections.Count);
        deserialized.Sections[0].Sections.Should().HaveCount(original.Sections[0].Sections.Count);
        deserialized.Sections[0].Sections[0].Prompts.Should().HaveCount(original.Sections[0].Sections[0].Prompts.Count);
    }

    [Fact]
    public void Serialize_WithPromptHints_ShouldIncludeAllHints()
    {
        var document = new AprDocument { Sections = [new Section { Id = "section_001", Title = "Test", Prompts = [new Prompt { Id = "prompt_001", Label = "Email", Hints = new PromptHints { Placeholder = "you@example.com", ExpectedDataType = "email", SuggestedValues = ["test@test.com"], HelpText = "Enter email", ValidationPattern = @"^.+@.+\..+$" } }] }] };
        var json = Serializer.Serialize(document);
        json.Should().Contain("\"placeholder\": \"you@example.com\"");
        json.Should().Contain("\"expectedDataType\": \"email\"");
        json.Should().Contain("\"suggestedValues\"");
        json.Should().Contain("\"helpText\": \"Enter email\"");
        json.Should().Contain("\"validationPattern\"");
    }

    [Fact]
    public void Serialize_WithDateTime_ShouldUseIso8601Format()
    {
        var timestamp = new DateTime(2025, 11, 12, 14, 30, 0, DateTimeKind.Utc);
        Serializer.Serialize(new AprDocument { Metadata = new Metadata { Title = "Test", Created = timestamp, Modified = timestamp } })
            .Should().Contain("2025-11-12T14:30:00");
    }

    [Fact]
    public void Serialize_WithEmptyCollections_ShouldIncludeEmptyArrays() =>
        Serializer.Serialize(new AprDocument { Metadata = new Metadata { Title = "Test" }, Sections = [] }).Should().Contain("\"sections\": []");

    [Fact]
    public void Serialize_ShouldProducePrettyPrintedJson()
    {
        var json = Serializer.Serialize(new AprDocument { Metadata = new Metadata { Title = "Test" }, Sections = [] });
        json.Should().Contain("\n");
        json.Should().Contain("  ");
    }

    [Fact]
    public void Serialize_WithCaseConversion_ShouldUseCamelCase()
    {
        var json = Serializer.Serialize(new AprDocument { Metadata = new Metadata { Title = "Test" } });
        json.Should().Contain("\"documentType\"");
        json.Should().NotContain("\"DocumentType\"");
    }
}
