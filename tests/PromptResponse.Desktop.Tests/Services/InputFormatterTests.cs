using FluentAssertions;
using PromptResponse.Desktop.Services;
using Xunit;

namespace PromptResponse.Desktop.Tests.Services;

/// <summary>
/// Unit tests for InputFormatter service.
/// </summary>
public class InputFormatterTests
{
    #region Phone Number Formatting

    [Theory]
    [InlineData("5551234567", "(555) 123-4567")]
    [InlineData("555-123-4567", "(555) 123-4567")]
    [InlineData("(555)123-4567", "(555) 123-4567")]
    [InlineData("555.123.4567", "(555) 123-4567")]
    [InlineData("555 123 4567", "(555) 123-4567")]
    public void FormatPhone_WithTenDigits_ShouldFormatCorrectly(string input, string expected)
    {
        // Act
        var result = InputFormatter.FormatPhone(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("555", "555")]
    [InlineData("5551", "(555) 1")]
    [InlineData("555123", "(555) 123")]
    [InlineData("5551234", "(555) 123-4")]
    public void FormatPhone_WithPartialDigits_ShouldFormatProgressively(string input, string expected)
    {
        // Act
        var result = InputFormatter.FormatPhone(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("15551234567", "+1 (555) 123-4567")]
    [InlineData("445551234567", "+44 (555) 123-4567")]
    public void FormatPhone_WithCountryCode_ShouldIncludeCountryCode(string input, string expected)
    {
        // Act
        var result = InputFormatter.FormatPhone(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void FormatPhone_WithEmptyString_ShouldReturnEmpty()
    {
        // Act
        var result = InputFormatter.FormatPhone("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void FormatPhone_WithNonDigits_ShouldExtractAndFormat()
    {
        // Act
        var result = InputFormatter.FormatPhone("Call me: 555-123-4567!");

        // Assert
        result.Should().Be("(555) 123-4567");
    }

    #endregion

    #region SSN Formatting

    [Theory]
    [InlineData("123456789", "123-45-6789")]
    [InlineData("123-45-6789", "123-45-6789")]
    [InlineData("123 45 6789", "123-45-6789")]
    public void FormatSSN_WithNineDigits_ShouldFormatCorrectly(string input, string expected)
    {
        // Act
        var result = InputFormatter.FormatSSN(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("123", "123")]
    [InlineData("1234", "123-4")]
    [InlineData("12345", "123-45")]
    [InlineData("123456", "123-45-6")]
    public void FormatSSN_WithPartialDigits_ShouldFormatProgressively(string input, string expected)
    {
        // Act
        var result = InputFormatter.FormatSSN(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void FormatSSN_WithMoreThanNineDigits_ShouldTruncate()
    {
        // Act
        var result = InputFormatter.FormatSSN("1234567890");

        // Assert
        result.Should().Be("123-45-6789");
    }

    #endregion

    #region EIN Formatting

    [Theory]
    [InlineData("123456789", "12-3456789")]
    [InlineData("12-3456789", "12-3456789")]
    public void FormatEIN_WithNineDigits_ShouldFormatCorrectly(string input, string expected)
    {
        // Act
        var result = InputFormatter.FormatEIN(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("12", "12")]
    [InlineData("123", "12-3")]
    [InlineData("1234", "12-34")]
    public void FormatEIN_WithPartialDigits_ShouldFormatProgressively(string input, string expected)
    {
        // Act
        var result = InputFormatter.FormatEIN(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Credit Card Formatting

    [Theory]
    [InlineData("1234567890123456", "1234 5678 9012 3456")]
    [InlineData("1234-5678-9012-3456", "1234 5678 9012 3456")]
    [InlineData("1234 5678 9012 3456", "1234 5678 9012 3456")]
    public void FormatCreditCard_With16Digits_ShouldFormatCorrectly(string input, string expected)
    {
        // Act
        var result = InputFormatter.FormatCreditCard(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1234", "1234")]
    [InlineData("12345", "1234 5")]
    [InlineData("12345678", "1234 5678")]
    [InlineData("123456789", "1234 5678 9")]
    public void FormatCreditCard_WithPartialDigits_ShouldFormatProgressively(string input, string expected)
    {
        // Act
        var result = InputFormatter.FormatCreditCard(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void FormatCreditCard_WithMoreThan16Digits_ShouldTruncate()
    {
        // Act
        var result = InputFormatter.FormatCreditCard("12345678901234567890");

        // Assert
        result.Should().Be("1234 5678 9012 3456");
    }

    #endregion

    #region ZIP Code Formatting

    [Theory]
    [InlineData("12345", "12345")]
    [InlineData("123456789", "12345-6789")]
    [InlineData("12345-6789", "12345-6789")]
    public void FormatZipCode_ShouldFormatCorrectly(string input, string expected)
    {
        // Act
        var result = InputFormatter.FormatZipCode(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1234", "1234")]
    [InlineData("123456", "12345-6")]
    public void FormatZipCode_WithPartialDigits_ShouldFormatProgressively(string input, string expected)
    {
        // Act
        var result = InputFormatter.FormatZipCode(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Currency Formatting

    [Theory]
    [InlineData("1000", "1,000.00")]
    [InlineData("1000.5", "1,000.50")]
    [InlineData("1000.55", "1,000.55")]
    [InlineData("1234567.89", "1,234,567.89")]
    public void FormatCurrency_ShouldFormatWithCommasAndDecimals(string input, string expected)
    {
        // Act
        var result = InputFormatter.FormatCurrency(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void FormatCurrency_WithNegativeValue_ShouldPreserveSign()
    {
        // Act
        var result = InputFormatter.FormatCurrency("-1000.50");

        // Assert
        result.Should().Be("-1,000.50");
    }

    [Fact]
    public void FormatCurrency_WithExistingFormatting_ShouldReformat()
    {
        // Act
        var result = InputFormatter.FormatCurrency("$1,234.56");

        // Assert
        result.Should().Be("1,234.56");
    }

    [Fact]
    public void FormatCurrency_WithNoDigits_ShouldReturnEmpty()
    {
        // Act - "not a number" has no digits, so regex removes everything
        var result = InputFormatter.FormatCurrency("not a number");

        // Assert - returns empty when no digits found
        result.Should().BeEmpty();
    }

    [Fact]
    public void FormatCurrency_WithPartialDigits_ShouldFormat()
    {
        // Act - "abc123" has digits that will be parsed
        var result = InputFormatter.FormatCurrency("abc123");

        // Assert - extracts 123 and formats it
        result.Should().Be("123.00");
    }

    #endregion

    #region Generic Format Method

    [Theory]
    [InlineData("5551234567", "phone", "(555) 123-4567")]
    [InlineData("123456789", "ssn", "123-45-6789")]
    [InlineData("123456789", "SSN", "123-45-6789")]  // Case insensitive
    [InlineData("123456789", "ein", "12-3456789")]
    [InlineData("1234567890123456", "creditcard", "1234 5678 9012 3456")]
    [InlineData("123456789", "zipcode", "12345-6789")]
    [InlineData("1000", "currency", "1,000.00")]
    public void Format_WithDataType_ShouldUseCorrectFormatter(string input, string dataType, string expected)
    {
        // Act
        var result = InputFormatter.Format(input, dataType);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Format_WithUnknownDataType_ShouldReturnOriginal()
    {
        // Act
        var result = InputFormatter.Format("test input", "unknown");

        // Assert
        result.Should().Be("test input");
    }

    [Fact]
    public void Format_WithNullInput_ShouldReturnEmpty()
    {
        // Act
        var result = InputFormatter.Format(null, "phone");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Format_WithNullDataType_ShouldReturnOriginal()
    {
        // Act
        var result = InputFormatter.Format("test input", null);

        // Assert
        result.Should().Be("test input");
    }

    #endregion

    #region GetMaxDigits

    [Theory]
    [InlineData("phone", 11)]
    [InlineData("ssn", 9)]
    [InlineData("ein", 9)]
    [InlineData("creditcard", 16)]
    [InlineData("zipcode", 9)]
    public void GetMaxDigits_ShouldReturnCorrectLimit(string dataType, int expectedMax)
    {
        // Act
        var result = InputFormatter.GetMaxDigits(dataType);

        // Assert
        result.Should().Be(expectedMax);
    }

    [Fact]
    public void GetMaxDigits_WithUnknownType_ShouldReturnNull()
    {
        // Act
        var result = InputFormatter.GetMaxDigits("unknown");

        // Assert
        result.Should().BeNull();
    }

    #endregion
}
