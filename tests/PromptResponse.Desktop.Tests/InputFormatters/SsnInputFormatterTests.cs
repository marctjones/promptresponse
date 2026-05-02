using AwesomeAssertions;
using PromptResponse.Desktop.InputFormatters;
using Xunit;

namespace PromptResponse.Desktop.Tests.InputFormatters;

public class SsnInputFormatterTests
{
    private readonly SsnInputFormatter _formatter = new();

    [Theory]
    [InlineData("123456789",   "123-45-6789")]
    [InlineData("123-45-6789", "123-45-6789")]
    [InlineData("123",         "123")]
    [InlineData("12345",       "123-45")]
    [InlineData("1234567",     "123-45-67")]
    public void Format_DigitsOnly_ProducesCanonicalShape(string raw, string expected)
    {
        var result = _formatter.Format(raw, raw.Length);
        result.Text.Should().Be(expected);
    }

    [Theory]
    [InlineData("on file")]
    [InlineData("withheld")]
    [InlineData("see HR")]
    [InlineData("xxx-xx-xxxx")]
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

    [Fact]
    public void Format_CaretAnchorsToDigitOrdinal()
    {
        // Caret after 3 digits in "123456789" → caret after 3rd digit in "123-45-6789".
        var result = _formatter.Format("123456789", 3);
        result.Text.Should().Be("123-45-6789");
        result.CaretIndex.Should().Be(3);
    }
}
