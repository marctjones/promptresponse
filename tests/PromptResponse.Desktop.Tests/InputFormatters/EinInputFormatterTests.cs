using AwesomeAssertions;
using PromptResponse.Desktop.InputFormatters;
using Xunit;

namespace PromptResponse.Desktop.Tests.InputFormatters;

public class EinInputFormatterTests
{
    private readonly EinInputFormatter _formatter = new();

    [Theory]
    [InlineData("123456789",  "12-3456789")]
    [InlineData("12-3456789", "12-3456789")]
    [InlineData("12",         "12")]
    [InlineData("123",        "12-3")]
    public void Format_DigitsOnly_ProducesCanonicalShape(string raw, string expected)
    {
        _formatter.Format(raw, raw.Length).Text.Should().Be(expected);
    }

    [Theory]
    [InlineData("LLC pending")]
    [InlineData("not yet assigned")]
    [InlineData("applied for")]
    public void Format_FreeText_PassesThroughUnchanged(string raw)
    {
        _formatter.Format(raw, raw.Length).Text.Should().Be(raw);
    }

    [Fact]
    public void Format_TooManyDigits_PassesThrough()
    {
        var raw = "1234567890";
        _formatter.Format(raw, raw.Length).Text.Should().Be(raw);
    }
}
