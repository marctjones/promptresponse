using AwesomeAssertions;
using PromptResponse.Desktop.InputFormatters;
using Xunit;

namespace PromptResponse.Desktop.Tests.InputFormatters;

public class ZipCodeInputFormatterTests
{
    private readonly ZipCodeInputFormatter _formatter = new();

    [Theory]
    [InlineData("90210",      "90210")]
    [InlineData("902101234",  "90210-1234")]
    [InlineData("90210-1234", "90210-1234")]
    [InlineData("902",        "902")]
    public void Format_DigitsOnly_ProducesCanonicalShape(string raw, string expected)
    {
        _formatter.Format(raw, raw.Length).Text.Should().Be(expected);
    }

    [Theory]
    [InlineData("EC1A 1BB")]   // UK postal
    [InlineData("M5V 3M8")]    // Canadian postal
    [InlineData("PO Box 99")]  // free text
    public void Format_NonZipText_PassesThroughUnchanged(string raw)
    {
        _formatter.Format(raw, raw.Length).Text.Should().Be(raw);
    }
}
