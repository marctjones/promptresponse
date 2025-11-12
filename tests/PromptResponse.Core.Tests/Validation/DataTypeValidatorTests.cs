using FluentAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Validation;
using Xunit;

namespace PromptResponse.Core.Tests.Validation;

/// <summary>
/// Unit tests for DataTypeValidator.
/// </summary>
/// <remarks>
/// Note: Data type validation is advisory only. These tests verify that
/// the validator can detect type mismatches, but the application should
/// never prevent users from entering any text string.
/// </remarks>
public class DataTypeValidatorTests
{
    private readonly DataTypeValidator _validator;

    public DataTypeValidatorTests()
    {
        _validator = new DataTypeValidator();
    }

    [Fact]
    public void ValidateResponse_EmptyResponse_ShouldBeValid()
    {
        // Arrange
        var prompt = new Prompt
        {
            Id = "prompt_001",
            Label = "Email",
            Response = "",
            Hints = new PromptHints { ExpectedDataType = "email" }
        };

        // Act
        var result = _validator.ValidateResponse(prompt);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateResponse_NoExpectedType_ShouldBeValid()
    {
        // Arrange
        var prompt = new Prompt
        {
            Id = "prompt_001",
            Label = "Any Text",
            Response = "whatever",
            Hints = new PromptHints { ExpectedDataType = null }
        };

        // Act
        var result = _validator.ValidateResponse(prompt);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name+tag@domain.co.uk")]
    [InlineData("simple@test.org")]
    public void ValidateResponse_ValidEmail_ShouldBeValid(string email)
    {
        // Arrange
        var prompt = new Prompt
        {
            Response = email,
            Hints = new PromptHints { ExpectedDataType = "email" }
        };

        // Act
        var result = _validator.ValidateResponse(prompt);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    public void ValidateResponse_InvalidEmail_ShouldReturnWarning(string email)
    {
        // Arrange
        var prompt = new Prompt
        {
            Response = email,
            Hints = new PromptHints { ExpectedDataType = "email" }
        };

        // Act
        var result = _validator.ValidateResponse(prompt);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].ErrorCode.Should().Be("TYPE_MISMATCH");
    }

    [Theory]
    [InlineData("2025-11-12")]
    [InlineData("2000-01-01")]
    [InlineData("1990-12-31")]
    public void ValidateResponse_ValidDate_ShouldBeValid(string date)
    {
        // Arrange
        var prompt = new Prompt
        {
            Response = date,
            Hints = new PromptHints { ExpectedDataType = "date" }
        };

        // Act
        var result = _validator.ValidateResponse(prompt);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("13-32-2025")]
    [InlineData("2025/11/12")]
    public void ValidateResponse_InvalidDate_ShouldReturnWarning(string date)
    {
        // Arrange
        var prompt = new Prompt
        {
            Response = date,
            Hints = new PromptHints { ExpectedDataType = "date" }
        };

        // Act
        var result = _validator.ValidateResponse(prompt);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors[0].ErrorCode.Should().Be("TYPE_MISMATCH");
    }

    [Theory]
    [InlineData("42")]
    [InlineData("0")]
    [InlineData("-123")]
    [InlineData("3.14159")]
    public void ValidateResponse_ValidNumber_ShouldBeValid(string number)
    {
        // Arrange
        var prompt = new Prompt
        {
            Response = number,
            Hints = new PromptHints { ExpectedDataType = "number" }
        };

        // Act
        var result = _validator.ValidateResponse(prompt);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("12.34.56")]
    [InlineData("abc123")]
    public void ValidateResponse_InvalidNumber_ShouldReturnWarning(string number)
    {
        // Arrange
        var prompt = new Prompt
        {
            Response = number,
            Hints = new PromptHints { ExpectedDataType = "number" }
        };

        // Act
        var result = _validator.ValidateResponse(prompt);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://test.org/path?query=1")]
    [InlineData("ftp://files.example.com")]
    public void ValidateResponse_ValidUrl_ShouldBeValid(string url)
    {
        // Arrange
        var prompt = new Prompt
        {
            Response = url,
            Hints = new PromptHints { ExpectedDataType = "url" }
        };

        // Act
        var result = _validator.ValidateResponse(prompt);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("+1-555-0100")]
    [InlineData("555-1234")]
    [InlineData("(555) 123-4567")]
    public void ValidateResponse_Phone_ShouldAcceptVariousFormats(string phone)
    {
        // Arrange
        var prompt = new Prompt
        {
            Response = phone,
            Hints = new PromptHints { ExpectedDataType = "phone" }
        };

        // Act
        var result = _validator.ValidateResponse(prompt);

        // Assert - Phone validation is lenient
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateResponse_WithValidationPattern_ShouldMatch()
    {
        // Arrange
        var prompt = new Prompt
        {
            Response = "2025-11-12",
            Hints = new PromptHints
            {
                ValidationPattern = @"^\d{4}-\d{2}-\d{2}$"
            }
        };

        // Act
        var result = _validator.ValidateResponse(prompt);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateResponse_WithValidationPattern_NotMatching_ShouldReturnWarning()
    {
        // Arrange
        var prompt = new Prompt
        {
            Response = "invalid-format",
            Hints = new PromptHints
            {
                ValidationPattern = @"^\d{4}-\d{2}-\d{2}$"
            }
        };

        // Act
        var result = _validator.ValidateResponse(prompt);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors[0].ErrorCode.Should().Be("PATTERN_MISMATCH");
    }

    [Fact]
    public void ValidateResponse_UnknownDataType_ShouldBeValid()
    {
        // Arrange - Unknown types should not cause validation failures
        var prompt = new Prompt
        {
            Response = "anything",
            Hints = new PromptHints { ExpectedDataType = "custom-unknown-type" }
        };

        // Act
        var result = _validator.ValidateResponse(prompt);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateDocument_ShouldValidateAllPrompts()
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
                    Title = "Section",
                    Prompts = new List<Prompt>
                    {
                        new()
                        {
                            Id = "prompt_001",
                            Label = "Email",
                            Response = "invalid-email",
                            Hints = new PromptHints { ExpectedDataType = "email" }
                        },
                        new()
                        {
                            Id = "prompt_002",
                            Label = "Date",
                            Response = "not-a-date",
                            Hints = new PromptHints { ExpectedDataType = "date" }
                        }
                    }
                }
            }
        };

        // Act
        var result = _validator.ValidateDocument(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void ValidateDocument_WithSubsections_ShouldValidateAll()
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
                    Title = "Section",
                    Subsections = new List<Subsection>
                    {
                        new()
                        {
                            Id = "subsection_001",
                            Title = "Subsection",
                            Prompts = new List<Prompt>
                            {
                                new()
                                {
                                    Id = "prompt_001",
                                    Label = "Number",
                                    Response = "not-a-number",
                                    Hints = new PromptHints { ExpectedDataType = "number" }
                                }
                            }
                        }
                    }
                }
            }
        };

        // Act
        var result = _validator.ValidateDocument(document);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
    }

    [Fact]
    public void InferDataType_FromEmailResponse_ShouldReturnEmail()
    {
        // Act
        var inferredType = _validator.InferDataType("test@example.com");

        // Assert
        inferredType.Should().Be("email");
    }

    [Fact]
    public void InferDataType_FromDateResponse_ShouldReturnDate()
    {
        // Act
        var inferredType = _validator.InferDataType("2025-11-12");

        // Assert
        inferredType.Should().Be("date");
    }

    [Fact]
    public void InferDataType_FromNumberResponse_ShouldReturnNumber()
    {
        // Act
        var inferredType = _validator.InferDataType("42");

        // Assert
        inferredType.Should().Be("number");
    }

    [Fact]
    public void InferDataType_FromUrlResponse_ShouldReturnUrl()
    {
        // Act
        var inferredType = _validator.InferDataType("https://example.com");

        // Assert
        inferredType.Should().Be("url");
    }

    [Fact]
    public void InferDataType_FromPlainText_ShouldReturnText()
    {
        // Act
        var inferredType = _validator.InferDataType("Just some plain text");

        // Assert
        inferredType.Should().Be("text");
    }

    [Fact]
    public void InferDataType_FromMultilineText_ShouldReturnMultiline()
    {
        // Act
        var inferredType = _validator.InferDataType("Line 1\nLine 2\nLine 3");

        // Assert
        inferredType.Should().Be("multiline");
    }
}
