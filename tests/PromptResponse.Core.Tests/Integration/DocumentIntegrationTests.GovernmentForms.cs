using AwesomeAssertions;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Integration;

public partial class DocumentIntegrationTests
{
    [Fact]
    public void LoadIrsFormW4_ShouldDeserializeCorrectly()
    {
        // Arrange
        var document = ReadBeta6Fixture("irs-form-w4-2024.aprt");

        // Assert
        document.Should().NotBeNull();
        document.Version.Should().Be(AprFormat.CurrentVersion);
        document.DocumentType.Should().Be(DocumentType.Template);
        document.Metadata.Title.Should().Be("Form W-4: Employee's Withholding Certificate (2024)");
        document.Metadata.TemplateId.Should().Be("irs-w4-2024-v1");
        document.Metadata.Author.Should().Be("Internal Revenue Service");
        document.Sections.Should().HaveCount(6);

        var step1 = document.Sections[0];
        step1.Id.Should().Be("step1");
        step1.Title.Should().Be("Step 1: Enter Personal Information");
        step1.Prompts.Should().HaveCount(6);

        var step2 = document.Sections[1];
        step2.Id.Should().Be("step2");
        step2.Title.Should().Contain("Multiple Jobs");

        var step4 = document.Sections[3];
        step4.Id.Should().Be("step4");
    }

    [Fact]
    public void LoadGsaSf86_ShouldDeserializeComplexHierarchy()
    {
        // Arrange
        var document = ReadBeta6Fixture("gsa-sf86-sections.aprt");

        // Assert
        document.Should().NotBeNull();
        document.Version.Should().Be(AprFormat.CurrentVersion);
        document.DocumentType.Should().Be(DocumentType.Template);
        document.Metadata.Title.Should().Be("SF-86: Questionnaire for National Security Positions");
        document.Metadata.TemplateId.Should().Be("gsa-sf86-2024-v1");
        document.Metadata.Author.Should().Be("U.S. General Services Administration");
        document.Sections.Should().HaveCountGreaterThan(3);

        var section1 = document.Sections[0];
        section1.Id.Should().Be("section1");
        section1.Title.Should().Contain("Information About You");

        document.Sections.FirstOrDefault(s => s.Id == "section7").Should().NotBeNull();
        document.Sections.FirstOrDefault(s => s.Id == "section13a").Should().NotBeNull();
    }

    [Fact]
    public void LoadIrsForm1040_ShouldDeserializeWithCalculations()
    {
        // Arrange
        var document = ReadBeta6Fixture("irs-form-1040-simplified.aprt");

        // Assert
        document.Should().NotBeNull();
        document.Version.Should().Be(AprFormat.CurrentVersion);
        document.DocumentType.Should().Be(DocumentType.Template);
        document.Metadata.Title.Should().Be("Form 1040: U.S. Individual Income Tax Return (Simplified)");
        document.Metadata.TemplateId.Should().Be("irs-1040-2024-simplified-v1");
        document.Sections.Should().HaveCountGreaterThan(5);

        var filingStatus = document.Sections[0];
        filingStatus.Id.Should().Be("filing-status");
        filingStatus.Prompts.Should().ContainSingle();
        filingStatus.Prompts[0].Hints.SuggestedValues.Should().Contain("Single");

        document.Sections[1].Id.Should().Be("personal-info");
        document.Sections.FirstOrDefault(s => s.Id == "income").Should().NotBeNull();
    }

    [Fact]
    public void AllGovernmentForms_ShouldHaveProperMetadata()
    {
        // Arrange & Act
        var forms = new[]
        {
            ReadBeta6Fixture("irs-form-w4-2024.aprt"),
            ReadBeta6Fixture("gsa-sf86-sections.aprt"),
            ReadBeta6Fixture("irs-form-1040-simplified.aprt")
        };

        // Assert - All should have proper metadata
        foreach (var form in forms)
        {
            form.Version.Should().Be(AprFormat.CurrentVersion);
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
        var json = File.ReadAllText(GetExampleFilePath("irs-form-w4-2024.aprt"));
        var document = _serializer.Deserialize(json);

        // Act - Find prompts with type hints (recursively)
        var allPrompts = new List<Prompt>();
        CollectAllPrompts(document.Sections, allPrompts);

        // Assert
        allPrompts.Should().Contain(p => p.Hints.ExpectedDataType == "number");
        allPrompts.Should().Contain(p => p.Hints.ExpectedDataType == "date");
        allPrompts.Should().Contain(p => p.Hints.ExpectedDataType == "text");

        var datePrompts = allPrompts.Where(p => p.Hints.ExpectedDataType == "date").ToList();
        datePrompts.Should().NotBeEmpty();
        foreach (var datePrompt in datePrompts)
        {
            datePrompt.Hints.Placeholder.Should().NotBeNullOrEmpty();
        }
    }
}
