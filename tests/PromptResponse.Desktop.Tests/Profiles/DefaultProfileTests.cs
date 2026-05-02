using AwesomeAssertions;
using PromptResponse.Desktop.Profiles;
using Xunit;

namespace PromptResponse.Desktop.Tests.Profiles;

/// <summary>
/// The Default profile is the universal core: no enhancements, raw values, semantic
/// structure only. It IS the floor everyone starts from.
/// </summary>
public class DefaultProfileTests
{
    private readonly DefaultProfile _profile = new();

    [Theory]
    [InlineData("42000")]
    [InlineData("forty-two thousand")]
    [InlineData("2025-04-29")]
    [InlineData("April 29, 2025")]
    [InlineData("")]
    public void FormatDisplay_AlwaysReturnsRawInput(string input)
    {
        // The Default profile is identity for formatting — nothing is enhanced,
        // visual or otherwise. This is what every other profile builds on.
        _profile.FormatDisplay(input, "number").Should().Be(input);
        _profile.FormatDisplay(input, "currency").Should().Be(input);
        _profile.FormatDisplay(input, "date").Should().Be(input);
        _profile.FormatDisplay(input, null).Should().Be(input);
    }

    [Fact]
    public void Defaults_AreVisionAligned()
    {
        _profile.MinimumTouchTarget.Width.Should().Be(36);
        _profile.MinimumTouchTarget.Height.Should().Be(36);
        _profile.TextScale.Should().Be(1.0);
        _profile.AnimationsEnabled.Should().BeTrue();
        _profile.LiveRegions.Should().Be(LiveRegionVerbosity.Normal);
        _profile.TargetContrast.Should().Be(ContrastLevel.AA);
        _profile.ColorCuesEnabled.Should().BeTrue();
        _profile.ColorScheme.Should().Be(ColorScheme.Light);
    }

    [Fact]
    public void Name_IsStable_ForPersistedSettings()
    {
        _profile.Name.Should().Be("Default");
    }
}
