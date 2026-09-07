using AwesomeAssertions;
using PromptResponse.Host.Abstractions;
using Xunit;

namespace PromptResponse.HostAbstractions.Tests;

/// <summary>
/// <c>PromptResponse.Host.Abstractions</c> is deliberately interfaces and result types
/// with no implementation (see docs/ARCHITECTURE.md's "Host ports" section) — this is
/// the whole of what that project owns today, so this is the whole of what it needs
/// tested directly. Dependency-direction fitness tests over the same project live in
/// PromptResponse.Architecture.Tests.
/// </summary>
public class DeliveryResultTests
{
    [Fact]
    public void Delivered_reports_the_delivered_outcome_and_detail()
    {
        var result = DeliveryResult.Delivered("accepted by the target");

        result.Outcome.Should().Be(DeliveryOutcome.Delivered);
        result.Detail.Should().Be("accepted by the target");
    }

    [Fact]
    public void Refused_reports_the_refused_outcome_and_detail()
    {
        var result = DeliveryResult.Refused("target rejected the payload");

        result.Outcome.Should().Be(DeliveryOutcome.Refused);
        result.Detail.Should().Be("target rejected the payload");
    }

    [Fact]
    public void Unavailable_reports_the_unavailable_outcome_and_detail()
    {
        var result = DeliveryResult.Unavailable("this host has no mail client");

        result.Outcome.Should().Be(DeliveryOutcome.Unavailable);
        result.Detail.Should().Be("this host has no mail client");
    }

    [Theory]
    [InlineData(DeliveryOutcome.Delivered)]
    [InlineData(DeliveryOutcome.Refused)]
    [InlineData(DeliveryOutcome.Unavailable)]
    public void Every_outcome_is_reachable_through_its_own_factory(DeliveryOutcome outcome)
    {
        var result = outcome switch
        {
            DeliveryOutcome.Delivered => DeliveryResult.Delivered("x"),
            DeliveryOutcome.Refused => DeliveryResult.Refused("x"),
            DeliveryOutcome.Unavailable => DeliveryResult.Unavailable("x"),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };

        result.Outcome.Should().Be(outcome);
    }
}
