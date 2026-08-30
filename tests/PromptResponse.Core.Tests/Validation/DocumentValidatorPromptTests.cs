using AwesomeAssertions;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Validation;

/// <summary>Prompt validation invariants within a section.</summary>
public class DocumentValidatorPromptTests : DocumentValidatorTestBase
{
    [Fact]
    public void Validate_PromptWithEmptyId_ShouldReturnError()
    {
        var result = Validator.Validate(CreateDocument("Test", CreateSection(prompts: [new Prompt { Id = "", Label = "Question" }])));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath.Contains("prompts[0].id"));
    }

    [Fact]
    public void Validate_PromptWithEmptyLabel_ShouldReturnError()
    {
        var result = Validator.Validate(CreateDocument("Test", CreateSection(prompts: [new Prompt { Id = "prompt_001", Label = "" }])));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath.Contains("prompts[0].label"));
    }

    [Fact]
    public void Validate_DuplicatePromptIds_ShouldReturnError()
    {
        var result = Validator.Validate(CreateDocument(
            "Test",
            CreateSection(prompts:
            [
                new Prompt { Id = "prompt_001", Label = "Q1" },
                new Prompt { Id = "prompt_001", Label = "Q2" }
            ])));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }
}
