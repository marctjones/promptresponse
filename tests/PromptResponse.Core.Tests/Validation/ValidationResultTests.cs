using FluentAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Validation;
using Xunit;

namespace PromptResponse.Core.Tests.Validation;

/// <summary>
/// Unit tests for ValidationResult and ValidationError.
/// </summary>
public class ValidationResultTests
{
    [Fact]
    public void ValidationResult_Valid_ShouldHaveNoErrors()
    {
        // Arrange & Act
        var result = ValidationResult.Valid();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidationResult_WithError_ShouldBeInvalid()
    {
        // Arrange
        var error = new ValidationError("Test error", "field1");

        // Act
        var result = ValidationResult.Invalid(error);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Message.Should().Be("Test error");
        result.Errors[0].PropertyPath.Should().Be("field1");
    }

    [Fact]
    public void ValidationResult_WithMultipleErrors_ShouldContainAll()
    {
        // Arrange
        var errors = new[]
        {
            new ValidationError("Error 1", "field1"),
            new ValidationError("Error 2", "field2"),
            new ValidationError("Error 3", "field3")
        };

        // Act
        var result = ValidationResult.Invalid(errors);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(3);
    }

    [Fact]
    public void ValidationError_ShouldStoreMessageAndPath()
    {
        // Arrange & Act
        var error = new ValidationError("Missing required field", "metadata.title");

        // Assert
        error.Message.Should().Be("Missing required field");
        error.PropertyPath.Should().Be("metadata.title");
    }

    [Fact]
    public void ValidationError_WithErrorCode_ShouldStoreCode()
    {
        // Arrange & Act
        var error = new ValidationError("Invalid value", "sections[0].id", "INVALID_ID");

        // Assert
        error.Message.Should().Be("Invalid value");
        error.PropertyPath.Should().Be("sections[0].id");
        error.ErrorCode.Should().Be("INVALID_ID");
    }

    [Fact]
    public void ValidationError_WithoutErrorCode_ShouldHaveNullCode()
    {
        // Arrange & Act
        var error = new ValidationError("Test", "path");

        // Assert
        error.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void ValidationResult_ToString_ShouldFormatErrors()
    {
        // Arrange
        var result = ValidationResult.Invalid(
            new ValidationError("Error 1", "field1"),
            new ValidationError("Error 2", "field2")
        );

        // Act
        var str = result.ToString();

        // Assert
        str.Should().Contain("Error 1");
        str.Should().Contain("Error 2");
        str.Should().Contain("field1");
        str.Should().Contain("field2");
    }

    [Fact]
    public void ValidationResult_AddError_ShouldAddToErrors()
    {
        // Arrange
        var result = new ValidationResult();

        // Act
        result.AddError(new ValidationError("Test", "path"));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
    }

    [Fact]
    public void ValidationResult_AddMultipleErrors_ShouldAccumulate()
    {
        // Arrange
        var result = new ValidationResult();

        // Act
        result.AddError(new ValidationError("Error 1", "path1"));
        result.AddError(new ValidationError("Error 2", "path2"));

        // Assert
        result.Errors.Should().HaveCount(2);
    }

    // === Advisory warnings (vision: type/capacity hints never block) ===

    [Fact]
    public void ValidationResult_New_ShouldHaveEmptyWarnings()
    {
        var result = new ValidationResult();

        result.Warnings.Should().BeEmpty();
        result.HasWarnings.Should().BeFalse();
    }

    [Fact]
    public void ValidationResult_AddWarning_ShouldNotAffectIsValid()
    {
        var result = new ValidationResult();

        result.AddWarning(new ValidationWarning("Looks like 'five', expected number", "prompts[0]", "TYPE_MISMATCH"));

        result.IsValid.Should().BeTrue("warnings are advisory and never invalidate a document");
        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().ContainSingle();
    }

    [Fact]
    public void ValidationResult_AddWarning_AndAddError_AreIndependent()
    {
        var result = new ValidationResult();

        result.AddError(new ValidationError("Missing required field", "metadata.title"));
        result.AddWarning(new ValidationWarning("Looks like 'five', expected number", "prompts[0]", "TYPE_MISMATCH"));

        result.IsValid.Should().BeFalse("there is a structural error");
        result.HasWarnings.Should().BeTrue();
        result.Errors.Should().ContainSingle();
        result.Warnings.Should().ContainSingle();
    }

    [Fact]
    public void ValidationResult_AddMultipleWarnings_ShouldAccumulate()
    {
        var result = new ValidationResult();

        result.AddWarning(new ValidationWarning("W1", "p1", "TYPE_MISMATCH"));
        result.AddWarning(new ValidationWarning("W2", "p2", "PATTERN_MISMATCH"));

        result.Warnings.Should().HaveCount(2);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidationWarning_ShouldStoreMessagePathAndCode()
    {
        var warning = new ValidationWarning("'five' does not look like a number", "prompts[0]", "TYPE_MISMATCH");

        warning.Message.Should().Be("'five' does not look like a number");
        warning.PropertyPath.Should().Be("prompts[0]");
        warning.WarningCode.Should().Be("TYPE_MISMATCH");
    }

    [Fact]
    public void ValidationWarning_WithoutCode_ShouldHaveNullCode()
    {
        var warning = new ValidationWarning("Some advisory message", "path");

        warning.WarningCode.Should().BeNull();
    }

    [Fact]
    public void ValidationResult_ToString_ShouldIncludeWarnings()
    {
        var result = new ValidationResult();
        result.AddWarning(new ValidationWarning("Maybe a number?", "prompts[0]", "TYPE_MISMATCH"));

        var str = result.ToString();

        str.Should().Contain("Maybe a number?");
        str.Should().Contain("prompts[0]");
    }
}
