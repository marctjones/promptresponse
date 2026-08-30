using AwesomeAssertions;
using PromptResponse.Core;
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
        var document = CreateDocument(
            "Test Form",
            CreateSection(prompts: [new Prompt { Id = "prompt_001", Label = "Question 1", Response = "" }]));

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
        var document = CreateDocument("Test", version: "");

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
        var document = CreateDocument("Test", version: "99.0");

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
        var document = CreateDocument("");

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
        var document = CreateDocument("Test");

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
        var document = CreateDocument("Test", CreateSection(id: ""));

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
        var document = CreateDocument("Test", CreateSection(title: ""));

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
        var document = CreateDocument("Test", CreateSection());
        document.Sections.Add(CreateSection(title: "Section 2"));

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
        var document = CreateDocument(
            "Test",
            CreateSection(prompts: [new Prompt { Id = "", Label = "Question" }]));

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
        var document = CreateDocument(
            "Test",
            CreateSection(prompts: [new Prompt { Id = "prompt_001", Label = "" }]));

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
        var document = CreateDocument(
            "Test",
            CreateSection(prompts:
            [
                new Prompt { Id = "prompt_001", Label = "Q1" },
                new Prompt { Id = "prompt_001", Label = "Q2" }
            ]));

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
        var document = CreateDocument(
            "Test",
            CreateSection(childSections: [CreateSection(id: "", title: "Child Section")]));

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
        var document = CreateDocument("Test", CreateSection(title: "Empty Section"));

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
        var document = CreateDocument(
            "Test",
            CreateSection(title: "Section"),
            documentType: DocumentType.FilledForm);

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
            Version = AprFormat.CurrentVersion,
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
        var document = CreateDocument("", version: "");

        // Act
        var result = _validator.Validate(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void Validate_IrsFormW4Template_ShouldPassValidation()
    {
        ValidateFixtureShouldPass("irs-form-w4-2024.aprt");
    }

    [Fact]
    public void Validate_GsaSf86Template_ShouldPassValidation()
    {
        ValidateFixtureShouldPass("gsa-sf86-sections.aprt");
    }

    [Fact]
    public void Validate_IrsForm1040Template_ShouldPassValidation()
    {
        ValidateFixtureShouldPass("irs-form-1040-simplified.aprt");
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

        // Act & Assert
        foreach (var formFile in formFiles)
        {
            ValidateFixtureShouldPass(formFile);
        }
    }

    private void ValidateFixtureShouldPass(string filename)
    {
        var json = File.ReadAllText(GetExampleFilePath(filename));
        var document = new Core.Serialization.AprJsonSerializer().Deserialize(json);
        var result = _validator.Validate(document);

        result.IsValid.Should().BeTrue($"{filename} should pass validation");
        result.Errors.Should().BeEmpty($"{filename} should have no validation errors");
    }

    private static AprDocument CreateDocument(
        string title,
        Section? section = null,
        DocumentType documentType = DocumentType.Template,
        string version = AprFormat.CurrentVersion)
    {
        return new AprDocument
        {
            Version = version,
            DocumentType = documentType,
            Metadata = new Metadata { Title = title },
            Sections = section is null ? [] : [section]
        };
    }

    private static Section CreateSection(
        string id = "section_001",
        string title = "Section 1",
        List<Prompt>? prompts = null,
        List<Section>? childSections = null)
    {
        return new Section
        {
            Id = id,
            Title = title,
            Prompts = prompts ?? [],
            Sections = childSections ?? []
        };
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
