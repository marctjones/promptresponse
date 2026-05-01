using System.Globalization;
using FluentAssertions;
using PromptResponse.Desktop.InputFormatters;
using Xunit;

namespace PromptResponse.Desktop.Tests.InputFormatters;

public class CurrencyInputFormatterTests
{
    private readonly CurrencyInputFormatter _formatter = new(new CultureInfo("en-US"));

    [Theory]
    [InlineData("1234.56", "$1,234.56")]
    [InlineData("1234",    "$1,234.00")]
    [InlineData("0.5",     "$0.50")]
    [InlineData("$42",     "$42.00")]
    public void Format_NumericInput_RendersCurrency(string raw, string expected)
    {
        _formatter.Format(raw, raw.Length).Text.Should().Be(expected);
    }

    [Theory]
    [InlineData("varies")]
    [InlineData("see notes")]
    [InlineData("TBD")]
    [InlineData("approximately $1,000")]   // contains free text; passes through
    public void Format_FreeText_PassesThrough(string raw)
    {
        _formatter.Format(raw, raw.Length).Text.Should().Be(raw);
    }

    [Fact]
    public void Format_AlreadyFormatted_IsIdempotent()
    {
        var first = _formatter.Format("1234.56", 7);
        var second = _formatter.Format(first.Text, first.CaretIndex);
        second.Text.Should().Be(first.Text);
    }
}
