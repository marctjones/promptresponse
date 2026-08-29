using AwesomeAssertions;
using PromptResponse.Desktop.Services;
using Xunit;

namespace PromptResponse.Desktop.Tests.Services;

public class MailHandoffServiceTests
{
    [Theory]
    [InlineData("mailto:recipient@example.com", "recipient@example.com")]
    [InlineData("MAILTO:Recipient@example.com", "Recipient@example.com")]
    public void TryGetRecipient_AcceptsOnlyBareMailtoRecipient(string target, string expected)
    {
        MailHandoffService.TryGetRecipient(target, out var recipient).Should().BeTrue();
        recipient.Should().Be(expected);
    }

    [Theory]
    [InlineData("mailto:recipient@example.com?bcc=elsewhere@example.com")]
    [InlineData("mailto:recipient@example.com?subject=surprise")]
    [InlineData("mailto:recipient@example.com\u202E")]
    [InlineData("https://example.com/submit")]
    [InlineData("mailto:not an address")]
    public void TryGetRecipient_RejectsQueriesAndUnsafeTargets(string target)
    {
        MailHandoffService.TryGetRecipient(target, out _).Should().BeFalse();
    }

    [Fact]
    public void AppleMailScript_UsesArgumentsRatherThanInterpolatedAprValues()
    {
        MailHandoffService.AppleMailScript.Should().Contain("on run argv");
        MailHandoffService.AppleMailScript.Should().Contain("POSIX file attachmentPath");
        MailHandoffService.AppleMailScript.Should().Contain("make new attachment");
        MailHandoffService.AppleMailScript.Should().NotContain("mailto:");
    }
}
