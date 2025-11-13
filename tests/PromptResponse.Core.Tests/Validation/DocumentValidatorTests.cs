using FluentAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Validation;
using Xunit;

namespace PromptResponse.Core.Tests.Validation;

/// <summary>
/// Unit tests for DocumentValidator.
/// </summary>
public class DocumentValidatorTests
{
    private readonly DocumentValidator _validator;

    public DocumentValidatorTests()
    {
        _validator = new DocumentValidator();
    }

    [Fact]
    public void Validate_ValidDocument_ShouldReturnValid()
    {
        // Arrange
        var document = new AprDocument
        {
            Version = "1.0",
            DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "Test Form" },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "section_001",
                    Title = "Section 1",
                    Prompts = new List<Prompt>
                    {
                        new() { Id = "prompt_001", Label = "Question 1", Response = "" }
                    }
                }
            }
        };

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_NullDocument_ShouldReturnError()
    {
        // Act
        var result = _validator.Validate(null!);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Message.Should().Contain("null");
    }

    [Fact]
    public void Validate_MissingVersion_ShouldReturnError()
    {
        // Arrange
        var document = new AprDocument
        {
            Version = "",
            Metadata = new Metadata { Title = "Test" }
        };

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath == "version");
    }

    [Fact]
    public void Validate_UnsupportedVersion_ShouldReturnError()
    {
        // Arrange
        var document = new AprDocument
        {
            Version = "99.0",
            Metadata = new Metadata { Title = "Test" }
        };

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath == "version");
    }

    [Fact]
    public void Validate_EmptyTitle_ShouldReturnError()
    {
        // Arrange
        var document = new AprDocument
        {
            Metadata = new Metadata { Title = "" }
        };

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath == "metadata.title");
    }

    [Fact]
    public void Validate_NoSections_ShouldReturnError()
    {
        // Arrange
        var document = new AprDocument
        {
            Metadata = new Metadata { Title = "Test" },
            Sections = new List<Section>()
        };

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath == "sections");
    }

    [Fact]
    public void Validate_SectionWithEmptyId_ShouldReturnError()
    {
        // Arrange
        var document = new AprDocument
        {
            Metadata = new Metadata { Title = "Test" },
            Sections = new List<Section>
            {
                new() { Id = "", Title = "Section 1" }
            }
        };

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath.Contains("sections[0].id"));
    }

    [Fact]
    public void Validate_SectionWithEmptyTitle_ShouldReturnError()
    {
        // Arrange
        var document = new AprDocument
        {
            Metadata = new Metadata { Title = "Test" },
            Sections = new List<Section>
            {
                new() { Id = "section_001", Title = "" }
            }
        };

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath.Contains("sections[0].title"));
    }

    [Fact]
    public void Validate_DuplicateSectionIds_ShouldReturnError()
    {
        // Arrange
        var document = new AprDocument
        {
            Metadata = new Metadata { Title = "Test" },
            Sections = new List<Section>
            {
                new() { Id = "section_001", Title = "Section 1" },
                new() { Id = "section_001", Title = "Section 2" }
            }
        };

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_PromptWithEmptyId_ShouldReturnError()
    {
        // Arrange
        var document = new AprDocument
        {
            Metadata = new Metadata { Title = "Test" },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "section_001",
                    Title = "Section 1",
                    Prompts = new List<Prompt>
                    {
                        new() { Id = "", Label = "Question" }
                    }
                }
            }
        };

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath.Contains("prompts[0].id"));
    }

    [Fact]
    public void Validate_PromptWithEmptyLabel_ShouldReturnError()
    {
        // Arrange
        var document = new AprDocument
        {
            Metadata = new Metadata { Title = "Test" },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "section_001",
                    Title = "Section 1",
                    Prompts = new List<Prompt>
                    {
                        new() { Id = "prompt_001", Label = "" }
                    }
                }
            }
        };

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath.Contains("prompts[0].label"));
    }

    [Fact]
    public void Validate_DuplicatePromptIds_ShouldReturnError()
    {
        // Arrange
        var document = new AprDocument
        {
            Metadata = new Metadata { Title = "Test" },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "section_001",
                    Title = "Section 1",
                    Prompts = new List<Prompt>
                    {
                        new() { Id = "prompt_001", Label = "Q1" },
                        new() { Id = "prompt_001", Label = "Q2" }
                    }
                }
            }
        };

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_SubsectionWithEmptyId_ShouldReturnError()
    {
        // Arrange
        var document = new AprDocument
        {
            Metadata = new Metadata { Title = "Test" },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "section_001",
                    Title = "Section 1",
                    Subsections = new List<Subsection>
                    {
                        new() { Id = "", Title = "Subsection" }
                    }
                }
            }
        };

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath.Contains("subsections[0].id"));
    }

    [Fact]
    public void Validate_SectionWithNoPromptsOrSubsections_ShouldReturnError()
    {
        // Arrange
        var document = new AprDocument
        {
            Metadata = new Metadata { Title = "Test" },
            Sections = new List<Section>
            {
                new() { Id = "section_001", Title = "Empty Section" }
            }
        };

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("at least one prompt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_FilledFormWithoutTemplateId_ShouldReturnError()
    {
        // Arrange
        var document = new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata
            {
                Title = "Test",
                TemplateId = null
            },
            Sections = new List<Section>
            {
                new() { Id = "section_001", Title = "Section" }
            }
        };

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath == "metadata.templateId");
    }

    [Fact]
    public void Validate_ComplexValidDocument_ShouldReturnValid()
    {
        // Arrange
        var document = new AprDocument
        {
            Version = "1.0",
            DocumentType = DocumentType.Template,
            Metadata = new Metadata
            {
                Title = "Complex Form",
                Description = "A test form"
            },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "section_001",
                    Title = "Section 1",
                    Subsections = new List<Subsection>
                    {
                        new()
                        {
                            Id = "subsection_001_001",
                            Title = "Subsection 1",
                            Prompts = new List<Prompt>
                            {
                                new() { Id = "prompt_001", Label = "Q1" }
                            }
                        }
                    },
                    Prompts = new List<Prompt>
                    {
                        new() { Id = "prompt_002", Label = "Q2" }
                    }
                },
                new()
                {
                    Id = "section_002",
                    Title = "Section 2",
                    Prompts = new List<Prompt>
                    {
                        new() { Id = "prompt_003", Label = "Q3" }
                    }
                }
            }
        };

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MultipleErrors_ShouldReturnAll()
    {
        // Arrange
        var document = new AprDocument
        {
            Version = "",
            Metadata = new Metadata { Title = "" },
            Sections = new List<Section>()
        };

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(1);
    }
}
