using AwesomeAssertions;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Validation;

/// <summary>Section hierarchy validation invariants.</summary>
public class DocumentValidatorSectionTests : DocumentValidatorTestBase
{
    [Fact]
    public void Validate_SectionWithEmptyId_ShouldReturnError()
    {
        var result = Validator.Validate(CreateDocument("Test", CreateSection(id: "")));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath.Contains("sections[0].id"));
    }

    [Fact]
    public void Validate_SectionWithEmptyTitle_ShouldReturnError()
    {
        var result = Validator.Validate(CreateDocument("Test", CreateSection(title: "")));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath.Contains("sections[0].title"));
    }

    [Fact]
    public void Validate_DuplicateSectionIds_ShouldReturnError()
    {
        var document = CreateDocument("Test", CreateSection());
        document.Sections.Add(CreateSection(title: "Section 2"));

        var result = Validator.Validate(document);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ChildSectionWithEmptyId_ShouldReturnError()
    {
        var result = Validator.Validate(CreateDocument("Test", CreateSection(childSections: [CreateSection(id: "", title: "Child Section")])));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath.Contains("sections[0].id"));
    }

    [Fact]
    public void Validate_SectionWithNoPromptsOrChildSections_ShouldReturnError()
    {
        var result = Validator.Validate(CreateDocument("Test", CreateSection(title: "Empty Section")));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("at least one prompt", StringComparison.OrdinalIgnoreCase));
    }
}
