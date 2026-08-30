using AwesomeAssertions;
using Xunit;

namespace PromptResponse.Core.Tests.Validation;

public sealed class DataTypeValidatorExpectedTypeTests : DataTypeValidatorTestBase
{
    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name+tag@domain.co.uk")]
    [InlineData("simple@test.org")]
    public void ValidateResponse_ValidEmail_ShouldBeValid(string email) =>
        Validator.ValidateResponse(CreatePrompt(email, "email")).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    public void ValidateResponse_InvalidEmail_ShouldReturnWarning(string email)
    {
        var result = Validator.ValidateResponse(CreatePrompt(email, "email"));
        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().ContainSingle();
        result.Warnings[0].WarningCode.Should().Be("TYPE_MISMATCH");
    }

    [Theory]
    [InlineData("2025-11-12")]
    [InlineData("2000-01-01")]
    [InlineData("1990-12-31")]
    public void ValidateResponse_ValidDate_ShouldBeValid(string date) =>
        Validator.ValidateResponse(CreatePrompt(date, "date")).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("13-32-2025")]
    [InlineData("2025/11/12")]
    public void ValidateResponse_InvalidDate_ShouldReturnWarning(string date) =>
        Validator.ValidateResponse(CreatePrompt(date, "date")).HasWarnings.Should().BeTrue();

    [Theory]
    [InlineData("42")]
    [InlineData("0")]
    [InlineData("-123")]
    [InlineData("3.14159")]
    public void ValidateResponse_ValidNumber_ShouldBeValid(string number) =>
        Validator.ValidateResponse(CreatePrompt(number, "number")).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("12.34.56")]
    [InlineData("abc123")]
    public void ValidateResponse_InvalidNumber_ShouldReturnWarning(string number) =>
        Validator.ValidateResponse(CreatePrompt(number, "number")).HasWarnings.Should().BeTrue();

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://test.org/path?query=1")]
    [InlineData("ftp://files.example.com")]
    public void ValidateResponse_ValidUrl_ShouldBeValid(string url) =>
        Validator.ValidateResponse(CreatePrompt(url, "url")).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("+1-555-0100")]
    [InlineData("555-1234")]
    [InlineData("(555) 123-4567")]
    public void ValidateResponse_Phone_ShouldAcceptVariousFormats(string phone) =>
        Validator.ValidateResponse(CreatePrompt(phone, "phone")).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("2000-02-29")]
    [InlineData("2024-02-29")]
    [InlineData("1900-01-01")]
    [InlineData("2099-12-31")]
    public void ValidateResponse_EdgeCaseDates_ShouldBeValid(string date) =>
        Validator.ValidateResponse(CreatePrompt(date, "date")).IsValid.Should().BeTrue($"{date} should be a valid date");

    [Theory]
    [InlineData("2001-02-29")]
    [InlineData("2025-02-30")]
    [InlineData("2025-13-01")]
    [InlineData("2025-00-15")]
    [InlineData("2025-06-31")]
    public void ValidateResponse_InvalidEdgeCaseDates_ShouldReturnWarning(string date) =>
        Validator.ValidateResponse(CreatePrompt(date, "date")).HasWarnings.Should().BeTrue($"{date} should produce a warning");

    [Theory]
    [InlineData("1e10")]
    [InlineData("1.23e-4")]
    [InlineData("999999999999")]
    [InlineData("0.0000001")]
    [InlineData("+42")]
    [InlineData("  123  ")]
    public void ValidateResponse_EdgeCaseNumbers_ShouldBeValid(string number) =>
        Validator.ValidateResponse(CreatePrompt(number, "number")).IsValid.Should().BeTrue($"{number} should be a valid number");

    [Theory]
    [InlineData("1,234")]
    [InlineData("$99.99")]
    [InlineData("50%")]
    [InlineData("1/2")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void ValidateResponse_InvalidNumbers_ShouldReturnWarning(string number) =>
        Validator.ValidateResponse(CreatePrompt(number, "number")).HasWarnings.Should().BeTrue($"{number} should produce a warning");

    [Theory]
    [InlineData("user+tag@example.com")]
    [InlineData("very.long.email.address.with.many.dots@example.com")]
    [InlineData("user_name@example-domain.com")]
    [InlineData("123@example.com")]
    [InlineData("a@b.co")]
    public void ValidateResponse_ComplexValidEmails_ShouldBeValid(string email) =>
        Validator.ValidateResponse(CreatePrompt(email, "email")).IsValid.Should().BeTrue($"{email} should be a valid email");

    [Theory]
    [InlineData("user@")]
    [InlineData("@example.com")]
    [InlineData("user @example.com")]
    [InlineData("user@.com")]
    [InlineData("user@domain")]
    [InlineData("user..name@example.com")]
    [InlineData("user@domain..com")]
    public void ValidateResponse_InvalidEdgeCaseEmails_ShouldReturnWarning(string email) =>
        Validator.ValidateResponse(CreatePrompt(email, "email")).HasWarnings.Should().BeTrue($"{email} should produce a warning");

    [Theory]
    [InlineData("https://example.com:8080/path")]
    [InlineData("http://subdomain.example.com/path?q=1&p=2")]
    [InlineData("ftp://user:pass@example.com")]
    [InlineData("https://example.com/path#anchor")]
    [InlineData("http://192.168.1.1/admin")]
    public void ValidateResponse_ComplexValidUrls_ShouldBeValid(string url) =>
        Validator.ValidateResponse(CreatePrompt(url, "url")).IsValid.Should().BeTrue($"{url} should be a valid URL");

    [Theory]
    [InlineData("")]
    [InlineData("000-000-0000")]
    [InlineData("1234567890")]
    [InlineData("+1 (555) 123-4567")]
    [InlineData("555.123.4567")]
    public void ValidateResponse_PhoneNumbers_ShouldBeLenient(string phone) =>
        Validator.ValidateResponse(CreatePrompt(phone, "phone")).IsValid.Should().BeTrue($"{phone} should be accepted (lenient validation)");

    [Theory]
    [InlineData("2025-11-12T14:30:00Z")]
    [InlineData("2025-11-12T14:30:00")]
    [InlineData("2025-11-12 14:30:00")]
    public void ValidateResponse_DateTimeFormats_ShouldBeValidated(string datetime) =>
        Validator.ValidateResponse(CreatePrompt(datetime, "date")).Should().NotBeNull();
}
