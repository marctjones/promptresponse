using System.Globalization;
using AwesomeAssertions;
using PromptResponse.Desktop.InputFormatters;
using Xunit;

namespace PromptResponse.Desktop.Tests.InputFormatters;

public class PercentageInputFormatterTests
{
    private readonly PercentageInputFormatter _formatter = new(new CultureInfo("en-US"));

    [Theory]
    [InlineData("12",    "12%")]
    [InlineData("12.5",  "12.5%")]
    [InlineData("0.25",  "0.25%")]
    [InlineData("100",   "100%")]
    public void Format_Numeric_AppendsSuffix(string raw, string expected)
    {
        _formatter.Format(raw, raw.Length).Text.Should().Be(expected);
    }

    [Fact]
    public void Format_AlreadyHasSuffix_IsIdempotent()
    {
        _formatter.Format("12.5%", 5).Text.Should().Be("12.5%");
    }

    [Theory]
    [InlineData("most")]
    [InlineData("varies")]
    [InlineData("n/a")]
    public void Format_FreeText_PassesThrough(string raw)
    {
        _formatter.Format(raw, raw.Length).Text.Should().Be(raw);
    }
}
