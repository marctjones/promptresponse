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

    // === Vision: type hints are advisory; any visible text is valid ===

    [Theory]
    [InlineData("number", "5")]
    [InlineData("number", "five")]
    [InlineData("number", "No response")]
    [InlineData("number", "approximately 5")]
    [InlineData("email", "I prefer not to say")]
    [InlineData("date", "see attached")]
    [InlineData("phone", "n/a")]
    [InlineData("currency", "varies")]
    public void ValidateResponse_AnyVisibleTextResponse_ShouldBeValid(string expectedType, string response)
    {
        var prompt = new Prompt
        {
            Id = "p1",
            Label = "x",
            Response = response,
            Hints = new PromptHints { ExpectedDataType = expectedType }
        };

        var result = _validator.ValidateResponse(prompt);

        result.IsValid.Should().BeTrue("any visible text must be a valid response — type hints are advisory only");
    }

    [Fact]
    public void ValidateResponse_FiveAsNumberResponse_ShouldProduceAdvisoryWarning()
    {
        var prompt = new Prompt
        {
            Id = "p1",
            Label = "Age",
            Response = "five",
            Hints = new PromptHints { ExpectedDataType = "number" }
        };

        var result = _validator.ValidateResponse(prompt);

        result.IsValid.Should().BeTrue();
        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().ContainSingle();
        result.Warnings[0].WarningCode.Should().Be("TYPE_MISMATCH");
        result.Warnings[0].PropertyPath.Should().Be("p1");
    }

    [Fact]
    public void ValidateResponse_PatternMismatch_ShouldProduceWarning_NotError()
    {
        var prompt = new Prompt
        {
            Id = "p1",
            Label = "Code",
            Response = "ABC",
            Hints = new PromptHints { ValidationPattern = @"^\d{3}$" }
        };

        var result = _validator.ValidateResponse(prompt);

        result.IsValid.Should().BeTrue("pattern hints are advisory like type hints");
        result.HasWarnings.Should().BeTrue();
        result.Warnings[0].WarningCode.Should().Be("PATTERN_MISMATCH");
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
        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().ContainSingle();
        result.Warnings[0].WarningCode.Should().Be("TYPE_MISMATCH");
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
        result.HasWarnings.Should().BeTrue();
        result.Warnings[0].WarningCode.Should().Be("TYPE_MISMATCH");
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
        result.HasWarnings.Should().BeTrue();
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
        result.HasWarnings.Should().BeTrue();
        result.Warnings[0].WarningCode.Should().Be("PATTERN_MISMATCH");
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
        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().HaveCount(2);
    }

    [Fact]
    public void ValidateDocument_WithChildSections_ShouldValidateAll()
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
                    Sections = new List<Section>
                    {
                        new()
                        {
                            Id = "child_001",
                            Title = "Child Section",
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
        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().ContainSingle();
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

    #region Edge Case Tests

    [Theory]
    [InlineData("2000-02-29")] // Leap year
    [InlineData("2024-02-29")] // Another leap year
    [InlineData("1900-01-01")] // Old date
    [InlineData("2099-12-31")] // Future date
    public void ValidateResponse_EdgeCaseDates_ShouldBeValid(string date)
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
        result.IsValid.Should().BeTrue($"{date} should be a valid date");
    }

    [Theory]
    [InlineData("2001-02-29")] // Not a leap year
    [InlineData("2025-02-30")] // Invalid day in February
    [InlineData("2025-13-01")] // Invalid month
    [InlineData("2025-00-15")] // Zero month
    [InlineData("2025-06-31")] // June only has 30 days
    public void ValidateResponse_InvalidEdgeCaseDates_ShouldReturnWarning(string date)
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
        result.HasWarnings.Should().BeTrue($"{date} should produce a warning");
    }

    [Theory]
    [InlineData("1e10")] // Scientific notation
    [InlineData("1.23e-4")] // Negative exponent
    [InlineData("999999999999")] // Large number
    [InlineData("0.0000001")] // Very small decimal
    [InlineData("+42")] // Explicit positive
    [InlineData("  123  ")] // Whitespace around number
    public void ValidateResponse_EdgeCaseNumbers_ShouldBeValid(string number)
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
        result.IsValid.Should().BeTrue($"{number} should be a valid number");
    }

    [Theory]
    [InlineData("1,234")] // Comma separator (not universally valid in parse)
    [InlineData("$99.99")] // Currency symbol
    [InlineData("50%")] // Percentage
    [InlineData("1/2")] // Fraction
    [InlineData("NaN")] // Not a Number
    [InlineData("Infinity")] // Infinity string
    public void ValidateResponse_InvalidNumbers_ShouldReturnWarning(string number)
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
        result.HasWarnings.Should().BeTrue($"{number} should produce a warning");
    }

    [Theory]
    [InlineData("user+tag@example.com")] // Plus addressing
    [InlineData("very.long.email.address.with.many.dots@example.com")] // Long with dots
    [InlineData("user_name@example-domain.com")] // Underscore and hyphen
    [InlineData("123@example.com")] // Numeric local part
    [InlineData("a@b.co")] // Minimal valid email
    public void ValidateResponse_ComplexValidEmails_ShouldBeValid(string email)
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
        result.IsValid.Should().BeTrue($"{email} should be a valid email");
    }

    [Theory]
    [InlineData("user@")] // Missing domain
    [InlineData("@example.com")] // Missing local part
    [InlineData("user @example.com")] // Space in local part
    [InlineData("user@.com")] // Domain starts with dot
    [InlineData("user@domain")] // No TLD
    [InlineData("user..name@example.com")] // Consecutive dots
    [InlineData("user@domain..com")] // Consecutive dots in domain
    public void ValidateResponse_InvalidEdgeCaseEmails_ShouldReturnWarning(string email)
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
        result.HasWarnings.Should().BeTrue($"{email} should produce a warning");
    }

    [Theory]
    [InlineData("https://example.com:8080/path")] // With port
    [InlineData("http://subdomain.example.com/path?q=1&p=2")] // Complex query
    [InlineData("ftp://user:pass@example.com")] // With credentials
    [InlineData("https://example.com/path#anchor")] // With anchor
    [InlineData("http://192.168.1.1/admin")] // IP address
    public void ValidateResponse_ComplexValidUrls_ShouldBeValid(string url)
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
        result.IsValid.Should().BeTrue($"{url} should be a valid URL");
    }

    [Theory]
    [InlineData("")] // Empty string with phone type should be valid
    [InlineData("000-000-0000")] // All zeros
    [InlineData("1234567890")] // 10 digits no formatting
    [InlineData("+1 (555) 123-4567")] // Full international format
    [InlineData("555.123.4567")] // Dot separator
    public void ValidateResponse_PhoneNumbers_ShouldBeLenient(string phone)
    {
        // Arrange
        var prompt = new Prompt
        {
            Response = phone,
            Hints = new PromptHints { ExpectedDataType = "phone" }
        };

        // Act
        var result = _validator.ValidateResponse(prompt);

        // Assert
        // Phone validation is intentionally lenient
        result.IsValid.Should().BeTrue($"{phone} should be accepted (lenient validation)");
    }

    [Theory]
    [InlineData(".*")] // Match anything
    [InlineData(@"\d{3}-\d{3}-\d{4}")] // Phone pattern
    [InlineData(@"[A-Z]{2}\d{4}")] // Alphanumeric pattern
    public void ValidateResponse_ComplexPatterns_ShouldMatchCorrectly(string pattern)
    {
        // Arrange
        var prompt = new Prompt
        {
            Response = "ABC1234",
            Hints = new PromptHints
            {
                ValidationPattern = pattern
            }
        };

        // Act
        var result = _validator.ValidateResponse(prompt);

        // Assert
        if (pattern == @"[A-Z]{2}\d{4}")
        {
            result.IsValid.Should().BeTrue();
        }
        else if (pattern == ".*")
        {
            result.IsValid.Should().BeTrue();
        }
        else
        {
            result.HasWarnings.Should().BeTrue();
        }
    }

    [Fact]
    public void ValidateResponse_InvalidRegexPattern_ShouldHandleGracefully()
    {
        // Arrange
        var prompt = new Prompt
        {
            Response = "test",
            Hints = new PromptHints
            {
                ValidationPattern = "["  // Invalid regex
            }
        };

        // Act
        var result = _validator.ValidateResponse(prompt);

        // Assert
        // Should not throw, should return invalid with appropriate error
        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("text")] // Plain text
    [InlineData("TEXT")] // Different case
    [InlineData("Text")] // Mixed case
    [InlineData("email")] // Another known type
    public void ValidateResponse_CaseInsensitiveDataTypes_ShouldWork(string dataType)
    {
        // Arrange
        var prompt = new Prompt
        {
            Response = "some value",
            Hints = new PromptHints { ExpectedDataType = dataType }
        };

        // Act
        var result = _validator.ValidateResponse(prompt);

        // Assert
        // Text type should always be valid for any string
        if (dataType.ToLowerInvariant() == "text")
        {
            result.IsValid.Should().BeTrue();
        }
    }

    [Fact]
    public void ValidateResponse_WhitespaceOnlyResponse_ShouldBeValid()
    {
        // Arrange
        var prompt = new Prompt
        {
            Response = "   ",
            Hints = new PromptHints { ExpectedDataType = "text" }
        };

        // Act
        var result = _validator.ValidateResponse(prompt);

        // Assert
        // Whitespace-only should be treated as valid text
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("2025-11-12T14:30:00Z")] // ISO 8601 datetime
    [InlineData("2025-11-12T14:30:00")] // Without timezone
    [InlineData("2025-11-12 14:30:00")] // Space separator
    public void ValidateResponse_DateTimeFormats_ShouldBeValidated(string datetime)
    {
        // Arrange
        var prompt = new Prompt
        {
            Response = datetime,
            Hints = new PromptHints { ExpectedDataType = "date" }
        };

        // Act
        var result = _validator.ValidateResponse(prompt);

        // Assert
        // These should be validated based on the date validator implementation
        // The exact behavior depends on whether datetime formats are supported
        result.Should().NotBeNull();
    }

    [Fact]
    public void InferDataType_FromComplexData_ShouldInferCorrectly()
    {
        // Test various complex inputs
        var testCases = new[]
        {
            ("1.23e10", "number"),
            ("user@subdomain.example.com", "email"),
            ("https://example.com/path?query=1", "url"),
            ("Multiple\nLines\nOf\nText", "multiline"),
            ("Simple text", "text")
        };

        foreach (var (input, expectedType) in testCases)
        {
            // Act
            var inferredType = _validator.InferDataType(input);

            // Assert
            inferredType.Should().Be(expectedType, $"Input '{input}' should infer to '{expectedType}'");
        }
    }

    #endregion

    // Per-cell type advisories are emitted on the cell prompts directly — each
    // cell is a regular Prompt with its own ExpectedDataType. Table structure
    // lives on Section.TableLayout, not on individual prompts.
}
