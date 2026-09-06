using AwesomeAssertions;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Models;

/// <summary>
/// Unit tests for the Metadata model.
/// </summary>
public class MetadataTests
{
    [Fact]
    public void Metadata_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var metadata = new Metadata();

        // Assert
        metadata.Title.Should().BeEmpty();
        metadata.Description.Should().BeNull();
        metadata.Created.Should().BeNull();
        metadata.Modified.Should().BeNull();
        metadata.Author.Should().BeNull();
        metadata.TemplateId.Should().BeNull();
        metadata.TemplateVersion.Should().BeNull();
    }

    [Fact]
    public void SetTitle_ShouldStoreValue()
    {
        // Arrange
        var metadata = new Metadata();
        const string expectedTitle = "Employment Application";

        // Act
        metadata.Title = expectedTitle;

        // Assert
        metadata.Title.Should().Be(expectedTitle);
    }

    [Fact]
    public void SetDescription_ShouldStoreValue()
    {
        // Arrange
        var metadata = new Metadata();
        const string expectedDescription = "Standard employment application form";

        // Act
        metadata.Description = expectedDescription;

        // Assert
        metadata.Description.Should().Be(expectedDescription);
    }

    [Fact]
    public void SetCreated_ShouldStoreValue()
    {
        // Arrange
        var metadata = new Metadata();
        var expectedTime = DateTime.UtcNow;

        // Act
        metadata.Created = expectedTime;

        // Assert
        metadata.Created.Should().Be(expectedTime);
    }

    [Fact]
    public void SetModified_ShouldStoreValue()
    {
        // Arrange
        var metadata = new Metadata();
        var expectedTime = DateTime.UtcNow;

        // Act
        metadata.Modified = expectedTime;

        // Assert
        metadata.Modified.Should().Be(expectedTime);
    }

    [Fact]
    public void Metadata_ForTemplate_ShouldHaveTemplateFields()
    {
        // Arrange & Act
        var metadata = new Metadata
        {
            Title = "My Template",
            Description = "A test template",
            Created = DateTime.UtcNow,
            Modified = DateTime.UtcNow,
            Author = "John Doe",
            TemplateId = "tag:skpt.cl,2026:tests/template-001",
            TemplateVersion = "1.0"
        };

        // Assert
        metadata.Title.Should().Be("My Template");
        metadata.Author.Should().Be("John Doe");
        metadata.TemplateId.Should().Be("tag:skpt.cl,2026:tests/template-001");
        metadata.TemplateVersion.Should().Be("1.0");
    }

    [Fact]
    public void Metadata_ForFilledForm_ShouldHaveFilledFormFields()
    {
        // Arrange
        var filledDate = DateTime.UtcNow;

        // Act
        var metadata = new Metadata
        {
            Title = "Completed Form",
            TemplateId = "tag:skpt.cl,2026:tests/template-001",
            TemplateVersion = "1.0",
            Modified = DateTime.UtcNow
        };

        // Assert
        metadata.Title.Should().Be("Completed Form");
        metadata.TemplateId.Should().Be("tag:skpt.cl,2026:tests/template-001");
        metadata.TemplateVersion.Should().Be("1.0");
    }

    [Fact]
    public void SetAuthor_ShouldStoreValue()
    {
        // Arrange
        var metadata = new Metadata();
        const string expectedAuthor = "HR Department";

        // Act
        metadata.Author = expectedAuthor;

        // Assert
        metadata.Author.Should().Be(expectedAuthor);
    }

    [Fact]
    public void SetTemplateId_ShouldStoreValue()
    {
        // Arrange
        var metadata = new Metadata();
        const string expectedId = "employment-app-v1";

        // Act
        metadata.TemplateId = expectedId;

        // Assert
        metadata.TemplateId.Should().Be(expectedId);
    }

    [Fact]
    public void SetTemplateVersion_ShouldStoreValue()
    {
        // Arrange
        var metadata = new Metadata();
        const string expectedVersion = "2.1";

        // Act
        metadata.TemplateVersion = expectedVersion;

        // Assert
        metadata.TemplateVersion.Should().Be(expectedVersion);
    }

    [Fact]
    public void Metadata_WithAllNullableFieldsNull_ShouldBeValid()
    {
        // Arrange & Act
        var metadata = new Metadata
        {
            Title = "Minimal Metadata"
        };

        // Assert
        metadata.Title.Should().NotBeEmpty();
        metadata.Description.Should().BeNull();
        metadata.Author.Should().BeNull();
        metadata.TemplateId.Should().BeNull();
    }
}
