using AwesomeAssertions;
using Xunit;

namespace PromptResponse.Core.Tests.Validation;

public sealed class DataTypeValidatorInferenceTests : DataTypeValidatorTestBase
{
    [Theory]
    [InlineData("test@example.com", "email")]
    [InlineData("2025-11-12", "date")]
    [InlineData("42", "number")]
    [InlineData("https://example.com", "url")]
    [InlineData("Just some plain text", "text")]
    [InlineData("Line 1\nLine 2\nLine 3", "multiline")]
    public void InferDataType_FromRecognizedResponse_ShouldReturnExpectedType(string response, string expectedType)
    {
        Validator.InferDataType(response).Should().Be(expectedType);
    }

    [Fact]
    public void InferDataType_FromComplexData_ShouldInferCorrectly()
    {
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
            Validator.InferDataType(input).Should().Be(expectedType, $"Input '{input}' should infer to '{expectedType}'");
        }
    }
}
