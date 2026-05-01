using FluentAssertions;
using PromptResponse.Desktop.InputFormatters;
using Xunit;

namespace PromptResponse.Desktop.Tests.InputFormatters;

public class PhoneInputFormatterTests
{
    private readonly PhoneInputFormatter _formatter = new();

    [Theory]
    [InlineData("5551234567",         "(555) 123-4567")]
    [InlineData("(555) 123-4567",     "(555) 123-4567")]
    [InlineData("555-123-4567",       "(555) 123-4567")]
    [InlineData("555.123.4567",       "(555) 123-4567")]
    [InlineData("15551234567",        "+1 (555) 123-4567")]
    [InlineData("555",                "(555")]
    [InlineData("555123",             "(555) 123")]
    [InlineData("5551234",            "(555) 123-4")]
    public void Format_DigitsOnly_ProducesCanonicalShape(string raw, string expected)
    {
        var result = _formatter.Format(raw, raw.Length);
        result.Text.Should().Be(expected);
    }

    [Theory]
    [InlineData("see HR")]
    [InlineData("ext. 4242")]
    [InlineData("call switchboard")]
    [InlineData("five-five-five")]
    [InlineData("n/a")]
    public void Format_FreeText_PassesThroughUnchanged(string raw)
    {
        var result = _formatter.Format(raw, raw.Length);
        result.Text.Should().Be(raw);
    }

    [Fact]
    public void Format_EmptyString_PassesThrough()
    {
        var result = _formatter.Format(string.Empty, 0);
        result.Text.Should().Be(string.Empty);
    }

    [Fact]
    public void Format_TooManyDigits_PassesThrough()
    {
        // Anything longer than 11 digits is suspect — paste of an extension or
        // accidental concat. Don't truncate visibly; let the user fix it.
        var raw = "555123456789012";
        var result = _formatter.Format(raw, raw.Length);
        result.Text.Should().Be(raw);
    }

    [Fact]
    public void Format_PlacesCaretAfterLastTypedDigit()
    {
        // User typed "555" with caret at end (index 3); formatted is "(555".
        var result = _formatter.Format("555", 3);
        result.Text.Should().Be("(555");
        result.CaretIndex.Should().Be(4); // just after the third digit
    }

    [Fact]
    public void Format_CaretInMiddle_AnchorsToDigitsBefore()
    {
        // Raw "5551234567", caret after first 3 digits (index 3).
        // Formatted "(555) 123-4567"; caret should sit just after "(555".
        var result = _formatter.Format("5551234567", 3);
        result.Text.Should().Be("(555) 123-4567");
        result.CaretIndex.Should().Be(4);
    }

    [Fact]
    public void Format_AlreadyCanonical_DoesNotMoveCaret()
    {
        var raw = "(555) 123-4567";
        var result = _formatter.Format(raw, 5);
        result.Text.Should().Be(raw);
        result.CaretIndex.Should().Be(5);
    }
}
