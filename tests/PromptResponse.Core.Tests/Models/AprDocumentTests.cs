using AwesomeAssertions;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Models;

/// <summary>
/// Unit tests for the AprDocument model.
/// </summary>
public class AprDocumentTests
{
    [Fact]
    public void AprDocument_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var document = new AprDocument();

        // Assert
        document.Version.Should().Be(AprFormat.CurrentVersion);
        document.DocumentType.Should().Be(DocumentType.Template);
        document.Metadata.Should().NotBeNull();
        document.Sections.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void SetVersion_ShouldStoreValue()
    {
        // Arrange
        var document = new AprDocument();
        const string expectedVersion = "2.0";

        // Act
        document.Version = expectedVersion;

        // Assert
        document.Version.Should().Be(expectedVersion);
    }

    [Fact]
    public void SetDocumentType_ShouldStoreValue()
    {
        // Arrange
        var document = new AprDocument();

        // Act
        document.DocumentType = DocumentType.FilledForm;

        // Assert
        document.DocumentType.Should().Be(DocumentType.FilledForm);
    }

    [Fact]
    public void SetMetadata_ShouldStoreValue()
    {
        // Arrange
        var document = new AprDocument();
        var expectedMetadata = new Metadata { Title = "Test Form" };

        // Act
        document.Metadata = expectedMetadata;

        // Assert
        document.Metadata.Should().BeSameAs(expectedMetadata);
        document.Metadata.Title.Should().Be("Test Form");
    }

    [Fact]
    public void AddSection_ShouldAddToCollection()
    {
        // Arrange
        var document = new AprDocument();
        var section = new Section { Id = "section_001", Title = "Personal Info" };

        // Act
        document.Sections.Add(section);

        // Assert
        document.Sections.Should().ContainSingle()
            .Which.Should().BeSameAs(section);
    }

    [Fact]
    public void AddMultipleSections_ShouldMaintainOrder()
    {
        // Arrange
        var document = new AprDocument();
        var section1 = new Section { Id = "section_001", Title = "First" };
        var section2 = new Section { Id = "section_002", Title = "Second" };
        var section3 = new Section { Id = "section_003", Title = "Third" };

        // Act
        document.Sections.Add(section1);
        document.Sections.Add(section2);
        document.Sections.Add(section3);

        // Assert
        document.Sections.Should().HaveCount(3)
            .And.ContainInOrder(section1, section2, section3);
    }

    [Fact]
    public void AprDocument_AsTemplate_ShouldBeValid()
    {
        // Arrange & Act
        var document = new AprDocument
        {
            Version = AprFormat.CurrentVersion,
            DocumentType = DocumentType.Template,
            Metadata = new Metadata
            {
                Title = "Employment Application",
                Author = "HR Department",
                Created = DateTime.UtcNow,
                TemplateId = "employment-app-v1",
                TemplateVersion = "1.0"
            },
            Sections = new List<Section>
            {
                new() { Id = "section_001", Title = "Personal Information" }
            }
        };

        // Assert
        document.DocumentType.Should().Be(DocumentType.Template);
        document.Metadata.Title.Should().Be("Employment Application");
        document.Metadata.Author.Should().Be("HR Department");
        document.Sections.Should().ContainSingle();
    }

    [Fact]
    public void AprDocument_AsFilledForm_ShouldBeValid()
    {
        // Arrange & Act
        var document = new AprDocument
        {
            Version = AprFormat.CurrentVersion,
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata
            {
                Title = "Employment Application",
                TemplateId = "employment-app-v1",
                TemplateVersion = "1.0",
                FilledBy = "John Doe",
                FilledDate = DateTime.UtcNow,
                Modified = DateTime.UtcNow
            },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "section_001",
                    Title = "Personal Information",
                    Prompts = new List<Prompt>
                    {
                        new()
                        {
                            Id = "prompt_001",
                            Label = "Name",
                            Response = "John Doe"
                        }
                    }
                }
            }
        };

        // Assert
        document.DocumentType.Should().Be(DocumentType.FilledForm);
        document.Metadata.FilledBy.Should().Be("John Doe");
        document.Sections.Should().ContainSingle();
        document.Sections[0].Prompts.Should().ContainSingle();
        document.Sections[0].Prompts[0].Response.Should().Be("John Doe");
    }

    [Fact]
    public void AprDocument_WithComplexStructure_ShouldBeValid()
    {
        // Arrange & Act
        var document = new AprDocument
        {
            DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "Complex Form" },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "section_001",
                    Title = "Section with Child Sections",
                    Sections = new List<Section>
                    {
                        new()
                        {
                            Id = "child_001_001",
                            Title = "Child Section 1",
                            Prompts = new List<Prompt>
                            {
                                new() { Id = "prompt_001", Label = "Q1" }
                            }
                        },
                        new()
                        {
                            Id = "child_001_002",
                            Title = "Child Section 2",
                            Prompts = new List<Prompt>
                            {
                                new() { Id = "prompt_002", Label = "Q2" }
                            }
                        }
                    },
                    Prompts = new List<Prompt>
                    {
                        new() { Id = "prompt_003", Label = "Section-level prompt" }
                    }
                },
                new()
                {
                    Id = "section_002",
                    Title = "Simple Section",
                    Prompts = new List<Prompt>
                    {
                        new() { Id = "prompt_004", Label = "Q3" }
                    }
                }
            }
        };

        // Assert
        document.Sections.Should().HaveCount(2);
        document.Sections[0].Sections.Should().HaveCount(2);
        document.Sections[0].Prompts.Should().ContainSingle();
        document.Sections[1].Sections.Should().BeEmpty();
        document.Sections[1].Prompts.Should().ContainSingle();
    }

    [Fact]
    public void AprDocument_DefaultVersion_ShouldBeCurrentFormatVersion()
    {
        // Arrange & Act
        var document = new AprDocument();

        // Assert
        document.Version.Should().Be(AprFormat.CurrentVersion);
    }

    [Fact]
    public void AprDocument_DefaultDocumentType_ShouldBeTemplate()
    {
        // Arrange & Act
        var document = new AprDocument();

        // Assert
        document.DocumentType.Should().Be(DocumentType.Template);
    }
}
