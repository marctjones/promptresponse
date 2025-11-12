using FluentAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Core.Tests.Integration;

/// <summary>
/// Integration tests for the complete document lifecycle.
/// </summary>
public class DocumentIntegrationTests
{
    private readonly AprJsonSerializer _serializer;

    public DocumentIntegrationTests()
    {
        _serializer = new AprJsonSerializer();
    }

    [Fact]
    public void LoadSimpleContactForm_ShouldDeserializeCorrectly()
    {
        // Arrange
        var examplePath = GetExampleFilePath("simple-contact-form.apr");
        var json = File.ReadAllText(examplePath);

        // Act
        var document = _serializer.Deserialize(json);

        // Assert
        document.Should().NotBeNull();
        document.Version.Should().Be("1.0");
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
        var examplePath = GetExampleFilePath("employment-application.apr");
        var json = File.ReadAllText(examplePath);

        // Act
        var document = _serializer.Deserialize(json);

        // Assert
        document.Should().NotBeNull();
        document.Version.Should().Be("1.0");
        document.DocumentType.Should().Be(DocumentType.Template);
        document.Metadata.Title.Should().Be("Employment Application Form");
        document.Metadata.TemplateId.Should().Be("employment-app-v1");

        // Should have 4 sections
        document.Sections.Should().HaveCount(4);

        // First section should have subsections
        var personalInfoSection = document.Sections[0];
        personalInfoSection.Title.Should().Be("Personal Information");
        personalInfoSection.Subsections.Should().HaveCount(2);

        // Check subsection structure
        var nameContactSubsection = personalInfoSection.Subsections[0];
        nameContactSubsection.Title.Should().Be("Name and Contact");
        nameContactSubsection.Prompts.Should().HaveCount(3);

        // Section should also have direct prompts
        personalInfoSection.Prompts.Should().ContainSingle();
        personalInfoSection.Prompts[0].Label.Should().Be("Date of Birth");
    }

    [Fact]
    public void RoundTrip_WithEmploymentApplication_ShouldPreserveAllData()
    {
        // Arrange
        var examplePath = GetExampleFilePath("employment-application.apr");
        var originalJson = File.ReadAllText(examplePath);
        var original = _serializer.Deserialize(originalJson);

        // Act
        var serializedJson = _serializer.Serialize(original);
        var roundTripped = _serializer.Deserialize(serializedJson);

        // Assert
        roundTripped.Version.Should().Be(original.Version);
        roundTripped.DocumentType.Should().Be(original.DocumentType);
        roundTripped.Metadata.Title.Should().Be(original.Metadata.Title);
        roundTripped.Sections.Should().HaveCount(original.Sections.Count);

        // Check first section in detail
        roundTripped.Sections[0].Id.Should().Be(original.Sections[0].Id);
        roundTripped.Sections[0].Title.Should().Be(original.Sections[0].Title);
        roundTripped.Sections[0].Subsections.Should().HaveCount(
            original.Sections[0].Subsections.Count);
    }

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

        // Add responses
        filledForm.Sections[0].Prompts[0].Response = "John Doe";
        filledForm.Sections[0].Prompts[1].Response = "john.doe@example.com";
        filledForm.Sections[0].Prompts[2].Response = "This is a test message";

        // Serialize the filled form
        var filledJson = _serializer.Serialize(filledForm);

        // Deserialize it back
        var deserialized = _serializer.Deserialize(filledJson);

        // Assert
        deserialized.DocumentType.Should().Be(DocumentType.FilledForm);
        deserialized.Metadata.FilledBy.Should().Be("John Doe");
        deserialized.Sections[0].Prompts[0].Response.Should().Be("John Doe");
        deserialized.Sections[0].Prompts[1].Response.Should().Be("john.doe@example.com");
        deserialized.Sections[0].Prompts[2].Response.Should().Be("This is a test message");
    }

    [Fact]
    public async Task DeserializeAsync_WithEmploymentApplication_ShouldWork()
    {
        // Arrange
        var examplePath = GetExampleFilePath("employment-application.apr");
        using var stream = File.OpenRead(examplePath);

        // Act
        var document = await _serializer.DeserializeAsync(stream);

        // Assert
        document.Should().NotBeNull();
        document.Metadata.Title.Should().Be("Employment Application Form");
        document.Sections.Should().HaveCount(4);
    }

    [Fact]
    public async Task SerializeAsync_ShouldProduceValidJson()
    {
        // Arrange
        var document = new AprDocument
        {
            Metadata = new Metadata { Title = "Test Form" },
            Sections = new List<Section>
            {
                new() { Id = "section_001", Title = "Test Section" }
            }
        };

        using var stream = new MemoryStream();

        // Act
        await _serializer.SerializeAsync(document, stream);

        // Assert
        stream.Length.Should().BeGreaterThan(0);
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        json.Should().Contain("\"title\":\"Test Form\"");
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
        document.Metadata.Created.Value.Year.Should().Be(2025);
    }

    private static string GetExampleFilePath(string filename)
    {
        // Navigate from test output directory to examples directory
        var testDir = Directory.GetCurrentDirectory();
        var projectRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        var examplesDir = Path.Combine(projectRoot, "examples");
        return Path.Combine(examplesDir, filename);
    }
}
