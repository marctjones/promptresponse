using AwesomeAssertions;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Core.Tests.Serialization;

public sealed class AprJsonSerializerDeserializationTests : AprJsonSerializerTestBase
{
    [Fact]
    public void Deserialize_WithValidJson_ShouldCreateDocument()
    {
        var document = Serializer.Deserialize("""
        { "version": "1.0-beta.6", "documentType": "template", "metadata": { "title": "Test Form" }, "sections": [] }
        """);
        document.Should().NotBeNull();
        document.Version.Should().Be(AprFormat.CurrentVersion);
        document.DocumentType.Should().Be(DocumentType.Template);
        document.Metadata.Title.Should().Be("Test Form");
    }

    [Fact]
    public void Deserialize_WithFilledFormType_ShouldSetCorrectType()
    {
        var document = Serializer.Deserialize("""
        { "version": "1.0-beta.6", "documentType": "filledForm", "metadata": { "title": "Filled" }, "sections": [] }
        """);
        document.DocumentType.Should().Be(DocumentType.FilledForm);
    }

    [Fact]
    public void Deserialize_WithInvalidJson_ShouldThrowException()
    {
        var act = () => Serializer.Deserialize("{ invalid json }");
        act.Should().Throw<SerializationException>();
    }

    [Fact]
    public void Deserialize_WithRetiredVersion_ShouldThrowException()
    {
        var act = () => Serializer.Deserialize("""{ "version": "1.0-beta", "metadata": { "title": "Old" }, "sections": [] }""");
        act.Should().Throw<SerializationException>().WithMessage("*1.0-beta.6*");
    }

    [Fact]
    public void Deserialize_WithNullFields_ShouldHandleGracefully()
    {
        var document = Serializer.Deserialize("""
        {
          "version": "1.0-beta.6", "documentType": "template",
          "metadata": { "title": "Test", "description": null, "author": null },
          "sections": [{ "id": "section_001", "title": "Test", "description": null, "prompts": [] }]
        }
        """);
        document.Metadata.Description.Should().BeNull();
        document.Metadata.Author.Should().BeNull();
        document.Sections[0].Description.Should().BeNull();
    }

    [Fact]
    public void Deserialize_WithIso8601DateTime_ShouldParseCorrectly()
    {
        var document = Serializer.Deserialize("""
        { "version": "1.0-beta.6", "documentType": "template", "metadata": { "title": "Test", "created": "2025-11-12T14:30:00Z" }, "sections": [] }
        """);
        document.Metadata.Created.Should().NotBeNull();
        document.Metadata.Created!.Value.Year.Should().Be(2025);
        document.Metadata.Created!.Value.Month.Should().Be(11);
        document.Metadata.Created!.Value.Day.Should().Be(12);
    }
}
