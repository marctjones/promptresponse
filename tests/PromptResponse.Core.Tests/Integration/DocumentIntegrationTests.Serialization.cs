using AwesomeAssertions;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Integration;

public partial class DocumentIntegrationTests
{
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
        roundTripped.Sections[0].Id.Should().Be(original.Sections[0].Id);
        roundTripped.Sections[0].Title.Should().Be(original.Sections[0].Title);
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
        json.Should().Contain("\"title\": \"Test Form\"");
    }
}
