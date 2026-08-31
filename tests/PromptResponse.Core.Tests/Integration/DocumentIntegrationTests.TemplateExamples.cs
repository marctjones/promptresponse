using AwesomeAssertions;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Integration;

public partial class DocumentIntegrationTests
{
    [Fact]
    public void LoadSimpleContactForm_ShouldDeserializeCorrectly()
    {
        // Arrange
        var document = ReadBeta6Fixture("simple-contact-form.apr");

        // Assert
        document.Should().NotBeNull();
        document.Version.Should().Be(AprFormat.CurrentVersion);
        document.DocumentType.Should().Be(DocumentType.Template);
        document.Metadata.Title.Should().Be("Simple Contact Form");
        document.Metadata.TemplateId.Should().Be("simple-contact-v1");
        document.Sections.Should().ContainSingle();
        document.Sections[0].Title.Should().Be("Contact Information");
        document.Sections[0].Prompts.Should().HaveCount(3);
    }

    [Fact]
    public void LoadEmploymentApplication_ShouldDeserializeCompleteStructure()
    {
        // Arrange
        var document = ReadBeta6Fixture("employment-application.apr");

        // Assert
        document.Should().NotBeNull();
        document.Version.Should().Be(AprFormat.CurrentVersion);
        document.DocumentType.Should().Be(DocumentType.Template);
        document.Metadata.Title.Should().Be("Employment Application Form");
        document.Metadata.TemplateId.Should().Be("employment-app-v1");
        document.Sections.Should().HaveCount(4);

        var personalInfoSection = document.Sections[0];
        personalInfoSection.Title.Should().Be("Personal Information");
        personalInfoSection.Prompts.Should().ContainSingle();
        personalInfoSection.Prompts[0].Label.Should().Be("Date of Birth");
    }

    [Fact]
    public void PromptHints_ShouldSerializeCorrectly()
    {
        // Arrange
        var examplePath = GetExampleFilePath("employment-application.apr");
        var json = File.ReadAllText(examplePath);
        var document = _serializer.Deserialize(json);

        // Act - Find a prompt with suggestions
        var positionPrompt = document.Sections[1].Prompts
            .First(p => p.Label == "Position/Title");

        // Assert
        positionPrompt.Hints.SuggestedValues.Should().NotBeEmpty();
        positionPrompt.Hints.SuggestedValues.Should().Contain("Software Engineer");
        positionPrompt.Hints.SuggestedValues.Should().Contain("Manager");
    }

    [Fact]
    public void EmptyResponses_InTemplate_ShouldBeEmptyStrings()
    {
        // Arrange
        var examplePath = GetExampleFilePath("simple-contact-form.apr");
        var json = File.ReadAllText(examplePath);
        var document = _serializer.Deserialize(json);

        // Act & Assert
        foreach (var section in document.Sections)
        {
            foreach (var prompt in section.Prompts)
            {
                prompt.Response.Should().NotBeNull();
                prompt.Response.Should().BeEmpty();
            }
        }
    }

    [Fact]
    public void Metadata_Created_ShouldBeParsedAsDateTime()
    {
        // Arrange
        var examplePath = GetExampleFilePath("employment-application.apr");
        var json = File.ReadAllText(examplePath);
        var document = _serializer.Deserialize(json);

        // Act & Assert
        document.Metadata.Created.Should().NotBeNull();
        document.Metadata.Created!.Value.Year.Should().Be(2025);
    }
}
