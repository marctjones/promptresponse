using AwesomeAssertions;
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
    public void Validate_ChildSectionWithEmptyId_ShouldReturnError()
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
                    Sections = new List<Section>
                    {
                        new() { Id = "", Title = "Child Section" }
                    }
                }
            }
        };

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath.Contains("sections[0].id"));
    }

    [Fact]
    public void Validate_SectionWithNoPromptsOrChildSections_ShouldReturnError()
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

    [Fact]
    public void Validate_IrsFormW4Template_ShouldPassValidation()
    {
        // Arrange
        var examplePath = GetExampleFilePath("irs-form-w4-2024.aprt");
        var json = File.ReadAllText(examplePath);
        var serializer = new Core.Serialization.AprJsonSerializer();
        var document = serializer.Deserialize(json);

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_GsaSf86Template_ShouldPassValidation()
    {
        // Arrange
        var examplePath = GetExampleFilePath("gsa-sf86-sections.aprt");
        var json = File.ReadAllText(examplePath);
        var serializer = new Core.Serialization.AprJsonSerializer();
        var document = serializer.Deserialize(json);

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_IrsForm1040Template_ShouldPassValidation()
    {
        // Arrange
        var examplePath = GetExampleFilePath("irs-form-1040-simplified.aprt");
        var json = File.ReadAllText(examplePath);
        var serializer = new Core.Serialization.AprJsonSerializer();
        var document = serializer.Deserialize(json);

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_AllGovernmentForms_ShouldPassValidation()
    {
        // Arrange
        var formFiles = new[]
        {
            "irs-form-w4-2024.aprt",
            "gsa-sf86-sections.aprt",
            "irs-form-1040-simplified.aprt"
        };

        var serializer = new Core.Serialization.AprJsonSerializer();

        // Act & Assert
        foreach (var formFile in formFiles)
        {
            var examplePath = GetExampleFilePath(formFile);
            var json = File.ReadAllText(examplePath);
            var document = serializer.Deserialize(json);
            var result = _validator.Validate(document);

            result.IsValid.Should().BeTrue($"{formFile} should pass validation");
            result.Errors.Should().BeEmpty($"{formFile} should have no validation errors");
        }
    }

    private static string GetExampleFilePath(string filename)
    {
        // Test fixtures live in tests/Fixtures/ — the same set used by
        // DocumentIntegrationTests. Examples/ is for end-user demo files.
        var testDir = Directory.GetCurrentDirectory();
        var projectRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        var fixturesDir = Path.Combine(projectRoot, "tests", "Fixtures");
        return Path.Combine(fixturesDir, filename);
    }
}
