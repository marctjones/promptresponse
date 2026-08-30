using AwesomeAssertions;
using Xunit;

namespace PromptResponse.Core.Tests.Validation;

/// <summary>Tests the advisory contract: visible text is never rejected.</summary>
public sealed class DataTypeValidatorAdvisoryTests : DataTypeValidatorTestBase
{
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
        var result = Validator.ValidateResponse(CreatePrompt(response, expectedType, "p1", "x"));

        result.IsValid.Should().BeTrue("any visible text must be a valid response — type hints are advisory only");
    }

    [Fact]
    public void ValidateResponse_FiveAsNumberResponse_ShouldProduceAdvisoryWarning()
    {
        var result = Validator.ValidateResponse(CreatePrompt("five", "number", "p1", "Age"));

        result.IsValid.Should().BeTrue();
        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().ContainSingle();
        result.Warnings[0].WarningCode.Should().Be("TYPE_MISMATCH");
        result.Warnings[0].PropertyPath.Should().Be("p1");
    }

    [Fact]
    public void ValidateResponse_EmptyResponse_ShouldBeValid()
    {
        Validator.ValidateResponse(CreatePrompt("", "email", "prompt_001", "Email")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateResponse_NoExpectedType_ShouldBeValid()
    {
        Validator.ValidateResponse(CreatePrompt("whatever", null, "prompt_001", "Any Text")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateResponse_UnknownDataType_ShouldBeValid()
    {
        Validator.ValidateResponse(CreatePrompt("anything", "custom-unknown-type")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateResponse_WhitespaceOnlyResponse_ShouldBeValid()
    {
        Validator.ValidateResponse(CreatePrompt("   ", "text")).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("text")]
    [InlineData("TEXT")]
    [InlineData("Text")]
    [InlineData("email")]
    public void ValidateResponse_CaseInsensitiveDataTypes_ShouldWork(string dataType)
    {
        var result = Validator.ValidateResponse(CreatePrompt("some value", dataType));

        if (dataType.Equals("text", StringComparison.OrdinalIgnoreCase))
        {
            result.IsValid.Should().BeTrue();
        }
    }
}
