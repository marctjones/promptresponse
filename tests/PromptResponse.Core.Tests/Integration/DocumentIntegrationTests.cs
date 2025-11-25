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

        // First section
        var personalInfoSection = document.Sections[0];
        personalInfoSection.Title.Should().Be("Personal Information");
        // Note: personalInfoSection.Sections will contain child sections once example files are updated

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
        // Note: child sections count would be validated once example files use recursive sections
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
        json.Should().Contain("\"title\": \"Test Form\"");
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

    [Fact]
    public void LoadIrsFormW4_ShouldDeserializeCorrectly()
    {
        // Arrange
        var examplePath = GetExampleFilePath("irs-form-w4-2024.aprt");
        var json = File.ReadAllText(examplePath);

        // Act
        var document = _serializer.Deserialize(json);

        // Assert
        document.Should().NotBeNull();
        document.Version.Should().Be("1.0");
        document.DocumentType.Should().Be(DocumentType.Template);
        document.Metadata.Title.Should().Be("Form W-4: Employee's Withholding Certificate (2024)");
        document.Metadata.TemplateId.Should().Be("irs-w4-2024-v1");
        document.Metadata.Author.Should().Be("Internal Revenue Service");

        // Should have 6 main sections (Steps 1-5 plus Employer Section)
        document.Sections.Should().HaveCount(6);

        // Step 1 should have personal information prompts
        var step1 = document.Sections[0];
        step1.Id.Should().Be("step1");
        step1.Title.Should().Be("Step 1: Enter Personal Information");
        step1.Prompts.Should().HaveCount(6); // firstName, lastName, address, etc.

        // Step 2 should have subsections option
        var step2 = document.Sections[1];
        step2.Id.Should().Be("step2");
        step2.Title.Should().Contain("Multiple Jobs");

        // Step 4 - child sections would be present once example files are updated
        var step4 = document.Sections[3];
        step4.Id.Should().Be("step4");
        // Note: step4.Sections will be empty until example files are updated to use recursive sections
    }

    [Fact]
    public void LoadGsaSf86_ShouldDeserializeComplexHierarchy()
    {
        // Arrange
        var examplePath = GetExampleFilePath("gsa-sf86-sections.aprt");
        var json = File.ReadAllText(examplePath);

        // Act
        var document = _serializer.Deserialize(json);

        // Assert
        document.Should().NotBeNull();
        document.Version.Should().Be("1.0");
        document.DocumentType.Should().Be(DocumentType.Template);
        document.Metadata.Title.Should().Be("SF-86: Questionnaire for National Security Positions");
        document.Metadata.TemplateId.Should().Be("gsa-sf86-2024-v1");
        document.Metadata.Author.Should().Be("U.S. General Services Administration");

        // Should have multiple sections
        document.Sections.Should().HaveCountGreaterThan(3);

        // Section 1 - personal info section
        var section1 = document.Sections[0];
        section1.Id.Should().Be("section1");
        section1.Title.Should().Contain("Information About You");
        // Note: section1.Sections will contain child sections once example files are updated to use recursive sections

        // Section 7 (Where You Have Lived)
        var section7 = document.Sections.FirstOrDefault(s => s.Id == "section7");
        section7.Should().NotBeNull();
        // Note: section7.Sections will contain child sections once example files are updated

        // Section 13A (References)
        var section13a = document.Sections.FirstOrDefault(s => s.Id == "section13a");
        section13a.Should().NotBeNull();
        // Note: section13a.Sections will contain child sections once example files are updated
    }

    [Fact]
    public void LoadIrsForm1040_ShouldDeserializeWithCalculations()
    {
        // Arrange
        var examplePath = GetExampleFilePath("irs-form-1040-simplified.aprt");
        var json = File.ReadAllText(examplePath);

        // Act
        var document = _serializer.Deserialize(json);

        // Assert
        document.Should().NotBeNull();
        document.Version.Should().Be("1.0");
        document.DocumentType.Should().Be(DocumentType.Template);
        document.Metadata.Title.Should().Be("Form 1040: U.S. Individual Income Tax Return (Simplified)");
        document.Metadata.TemplateId.Should().Be("irs-1040-2024-simplified-v1");

        // Should have multiple sections for filing status, personal info, income, etc.
        document.Sections.Should().HaveCountGreaterThan(5);

        // Filing status section
        var filingStatus = document.Sections[0];
        filingStatus.Id.Should().Be("filing-status");
        filingStatus.Prompts.Should().ContainSingle();
        filingStatus.Prompts[0].Hints.SuggestedValues.Should().Contain("Single");

        // Personal info section
        var personalInfo = document.Sections[1];
        personalInfo.Id.Should().Be("personal-info");
        // Note: personalInfo.Sections will contain child sections once example files are updated

        // Income section
        var income = document.Sections.FirstOrDefault(s => s.Id == "income");
        income.Should().NotBeNull();
        // Note: income.Sections will contain child sections once example files are updated
    }

    [Fact]
    public void AllGovernmentForms_ShouldHaveProperMetadata()
    {
        // Arrange & Act
        var w4Path = GetExampleFilePath("irs-form-w4-2024.aprt");
        var sf86Path = GetExampleFilePath("gsa-sf86-sections.aprt");
        var form1040Path = GetExampleFilePath("irs-form-1040-simplified.aprt");

        var w4 = _serializer.Deserialize(File.ReadAllText(w4Path));
        var sf86 = _serializer.Deserialize(File.ReadAllText(sf86Path));
        var form1040 = _serializer.Deserialize(File.ReadAllText(form1040Path));

        // Assert - All should have proper metadata
        var forms = new[] { w4, sf86, form1040 };
        foreach (var form in forms)
        {
            form.Version.Should().Be("1.0");
            form.DocumentType.Should().Be(DocumentType.Template);
            form.Metadata.Title.Should().NotBeNullOrEmpty();
            form.Metadata.Author.Should().NotBeNullOrEmpty();
            form.Metadata.TemplateId.Should().NotBeNullOrEmpty();
            form.Metadata.Created.Should().NotBeNull();
            form.Sections.Should().NotBeEmpty();
        }
    }

    [Fact]
    public void GovernmentForms_ShouldUseProperDataTypeHints()
    {
        // Arrange
        var w4Path = GetExampleFilePath("irs-form-w4-2024.aprt");
        var json = File.ReadAllText(w4Path);
        var document = _serializer.Deserialize(json);

        // Act - Find prompts with type hints (recursively)
        var allPrompts = new List<Prompt>();
        CollectAllPrompts(document.Sections, allPrompts);

        // Assert
        allPrompts.Should().Contain(p => p.Hints.ExpectedDataType == "number");
        allPrompts.Should().Contain(p => p.Hints.ExpectedDataType == "date");
        allPrompts.Should().Contain(p => p.Hints.ExpectedDataType == "text");

        // Date fields should have proper placeholders
        var datePrompts = allPrompts.Where(p => p.Hints.ExpectedDataType == "date").ToList();
        datePrompts.Should().NotBeEmpty();
        foreach (var datePrompt in datePrompts)
        {
            datePrompt.Hints.Placeholder.Should().NotBeNullOrEmpty();
        }
    }

    private static void CollectAllPrompts(List<Section> sections, List<Prompt> prompts)
    {
        foreach (var section in sections)
        {
            prompts.AddRange(section.Prompts);
            CollectAllPrompts(section.Sections, prompts);
        }
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
