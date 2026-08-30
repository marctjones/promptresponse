using AwesomeAssertions;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Validation;

public sealed class DataTypeValidatorPatternAndDocumentTests : DataTypeValidatorTestBase
{
    [Fact]
    public void ValidateResponse_PatternMismatch_ShouldProduceWarning_NotError()
    {
        var result = Validator.ValidateResponse(CreatePrompt("ABC", validationPattern: @"^\d{3}$", id: "p1", label: "Code"));

        result.IsValid.Should().BeTrue("pattern hints are advisory like type hints");
        result.HasWarnings.Should().BeTrue();
        result.Warnings[0].WarningCode.Should().Be("PATTERN_MISMATCH");
    }

    [Fact]
    public void ValidateResponse_WithValidationPattern_ShouldMatch()
    {
        Validator.ValidateResponse(CreatePrompt("2025-11-12", validationPattern: @"^\d{4}-\d{2}-\d{2}$"))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateResponse_WithValidationPattern_NotMatching_ShouldReturnWarning()
    {
        var result = Validator.ValidateResponse(CreatePrompt("invalid-format", validationPattern: @"^\d{4}-\d{2}-\d{2}$"));

        result.HasWarnings.Should().BeTrue();
        result.Warnings[0].WarningCode.Should().Be("PATTERN_MISMATCH");
    }

    [Theory]
    [InlineData(".*")]
    [InlineData(@"\d{3}-\d{3}-\d{4}")]
    [InlineData(@"[A-Z]{2}\d{4}")]
    public void ValidateResponse_ComplexPatterns_ShouldMatchCorrectly(string pattern)
    {
        var result = Validator.ValidateResponse(CreatePrompt("ABC1234", validationPattern: pattern));

        if (pattern is ".*" or @"[A-Z]{2}\d{4}")
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
        var result = Validator.ValidateResponse(CreatePrompt("test", validationPattern: "["));

        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().NotBeEmpty();
    }

    [Fact]
    public void ValidateDocument_ShouldValidateAllPrompts()
    {
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
                        CreatePrompt("invalid-email", "email", "prompt_001", "Email"),
                        CreatePrompt("not-a-date", "date", "prompt_002", "Date")
                    }
                }
            }
        };

        var result = Validator.ValidateDocument(document);

        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().HaveCount(2);
    }

    [Fact]
    public void ValidateDocument_WithChildSections_ShouldValidateAll()
    {
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
                                CreatePrompt("not-a-number", "number", "prompt_001", "Number")
                            }
                        }
                    }
                }
            }
        };

        var result = Validator.ValidateDocument(document);

        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().ContainSingle();
    }
}
