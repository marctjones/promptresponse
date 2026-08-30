using AwesomeAssertions;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Integration;

public partial class DocumentIntegrationTests
{
    [Fact]
    public void CreateFilledForm_FromTemplate_ShouldWork()
    {
        // Arrange
        var examplePath = GetExampleFilePath("simple-contact-form.apr");
        var json = File.ReadAllText(examplePath);
        var template = _serializer.Deserialize(json);

        // Act - Fill out the form
        var filledForm = new AprDocument
        {
            Version = template.Version,
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata
            {
                Title = template.Metadata.Title,
                TemplateId = template.Metadata.TemplateId,
                TemplateVersion = template.Metadata.TemplateVersion,
                FilledBy = "John Doe",
                FilledDate = DateTime.UtcNow,
                Modified = DateTime.UtcNow
            },
            Sections = template.Sections
        };

        filledForm.Sections[0].Prompts[0].Response = "John Doe";
        filledForm.Sections[0].Prompts[1].Response = "john.doe@example.com";
        filledForm.Sections[0].Prompts[2].Response = "This is a test message";

        var filledJson = _serializer.Serialize(filledForm);
        var deserialized = _serializer.Deserialize(filledJson);

        // Assert
        deserialized.DocumentType.Should().Be(DocumentType.FilledForm);
        deserialized.Metadata.FilledBy.Should().Be("John Doe");
        deserialized.Sections[0].Prompts[0].Response.Should().Be("John Doe");
        deserialized.Sections[0].Prompts[1].Response.Should().Be("john.doe@example.com");
        deserialized.Sections[0].Prompts[2].Response.Should().Be("This is a test message");
    }
}
