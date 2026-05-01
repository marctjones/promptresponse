using FluentAssertions;
using PromptResponse.Desktop.InputFormatters;
using Xunit;

namespace PromptResponse.Desktop.Tests.InputFormatters;

public class InputFormatterRegistryTests
{
    [Theory]
    [InlineData("phone",      typeof(PhoneInputFormatter))]
    [InlineData("PHONE",      typeof(PhoneInputFormatter))]
    [InlineData("ssn",        typeof(SsnInputFormatter))]
    [InlineData("ein",        typeof(EinInputFormatter))]
    [InlineData("zipcode",    typeof(ZipCodeInputFormatter))]
    [InlineData("zip",        typeof(ZipCodeInputFormatter))]
    [InlineData("postalcode", typeof(ZipCodeInputFormatter))]
    [InlineData("currency",   typeof(CurrencyInputFormatter))]
    [InlineData("percentage", typeof(PercentageInputFormatter))]
    [InlineData("percent",    typeof(PercentageInputFormatter))]
    public void ForHint_KnownTypes_ReturnsFormatter(string hint, Type expected)
    {
        var f = InputFormatterRegistry.ForHint(hint);
        f.Should().NotBeNull();
        f!.GetType().Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("text")]
    [InlineData("multiline")]
    [InlineData("signature")]
    [InlineData("color")]
    [InlineData("gibberish")]
    public void ForHint_UnknownOrNoFormatterTypes_ReturnsNull(string? hint)
    {
        InputFormatterRegistry.ForHint(hint).Should().BeNull();
    }
}
