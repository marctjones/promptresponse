using AwesomeAssertions;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Validation;

/// <summary>Document-level validation invariants.</summary>
public class DocumentValidatorDocumentTests : DocumentValidatorTestBase
{
    [Fact]
    public void Validate_ValidDocument_ShouldReturnValid()
    {
        var document = CreateDocument("Test Form", CreateSection(prompts: [new Prompt { Id = "prompt_001", Label = "Question 1", Response = "" }]));

        var result = Validator.Validate(document);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_NullDocument_ShouldReturnError()
    {
        var result = Validator.Validate(null!);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Message.Should().Contain("null");
    }

    [Fact]
    public void Validate_MissingVersion_ShouldReturnError()
    {
        var result = Validator.Validate(CreateDocument("Test", version: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath == "version");
    }

    [Fact]
    public void Validate_UnsupportedVersion_ShouldReturnError()
    {
        var result = Validator.Validate(CreateDocument("Test", version: "99.0"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath == "version");
    }

    [Fact]
    public void Validate_EmptyTitle_ShouldReturnError()
    {
        var result = Validator.Validate(CreateDocument(""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath == "metadata.title");
    }

    [Fact]
    public void Validate_NoSections_ShouldReturnError()
    {
        var result = Validator.Validate(CreateDocument("Test"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath == "sections");
    }

    [Fact]
    public void Validate_FilledFormWithoutTemplateId_ShouldReturnError()
    {
        var result = Validator.Validate(CreateDocument("Test", CreateSection(title: "Section"), DocumentType.FilledForm));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath == "metadata.templateId");
    }

    [Fact]
    public void Validate_MultipleErrors_ShouldReturnAll()
    {
        var result = Validator.Validate(CreateDocument("", version: ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(1);
    }
}
