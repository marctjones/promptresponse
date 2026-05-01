using System.Globalization;
using Avalonia;
using FluentAssertions;
using PromptResponse.Desktop.Profiles;
using Xunit;

namespace PromptResponse.Desktop.Tests.Profiles;

/// <summary>
/// CompositeProfile merges multiple active profiles with deterministic precedence so
/// arbitrary user-selected combinations produce coherent merged behaviour. The merge
/// rule for each capability is "the most accommodating wins" — bigger touch targets,
/// higher contrast, larger text, more verbose live regions, no-animations beats
/// animations-on. ColorScheme is winner-take-all from a chosen primary profile.
/// </summary>
public class CompositeProfileTests
{
    [Fact]
    public void Compose_SingleProfile_BehavesLikeThatProfile()
    {
        var single = new HighContrastProfile();
        var composite = CompositeProfile.Of(single);

        composite.MinimumTouchTarget.Should().Be(single.MinimumTouchTarget);
        composite.AnimationsEnabled.Should().Be(single.AnimationsEnabled);
        composite.TargetContrast.Should().Be(single.TargetContrast);
        composite.TextScale.Should().Be(single.TextScale);
        composite.ColorScheme.Should().Be(single.ColorScheme);
    }

    [Fact]
    public void Compose_NoProfiles_FallsBackToDefault()
    {
        var composite = CompositeProfile.Of();

        composite.MinimumTouchTarget.Should().Be(new Size(36, 36));
        composite.AnimationsEnabled.Should().BeTrue();
        composite.TargetContrast.Should().Be(ContrastLevel.AA);
        composite.TextScale.Should().Be(1.0);
        composite.ColorScheme.Should().Be(ColorScheme.Light);
    }

    [Fact]
    public void Compose_HighContrastPlusLargeText_TakesAaaContrastAndLargerScale()
    {
        var composite = CompositeProfile.Of(new HighContrastProfile(), new LargeTextProfile());

        composite.TargetContrast.Should().Be(ContrastLevel.AAA, "HighContrast wins on contrast");
        composite.TextScale.Should().Be(1.5, "LargeText wins on text scale");
    }

    [Fact]
    public void Compose_LargeHitTargetsPlusNumberSeparators_TakesLargerTouchTargets_AndKeepsFormatting()
    {
        var composite = CompositeProfile.Of(
            new LargeHitTargetsProfile(),
            new NumberThousandsSeparatorsProfile(new CultureInfo("en-US")));

        composite.MinimumTouchTarget.Width.Should().Be(56, "LargeHitTargets wins on target size");
        composite.MinimumTouchTarget.Height.Should().Be(56);
        composite.FormatDisplay("42000", "number").Should().Be("42,000",
            "the per-flag display rule still applies under composition");
    }

    [Fact]
    public void Compose_AnyProfileWithAnimationsDisabled_DisablesAnimationsForAll()
    {
        // Reduced-motion semantics: if ANY active profile says "no animations", the
        // composite respects that, even if other profiles would normally allow them.
        var composite = CompositeProfile.Of(new LightProfile(), new ReducedMotionProfile());

        composite.AnimationsEnabled.Should().BeFalse(
            "any profile disabling animations must win — ReducedMotion is a capability hard-stop");
    }

    [Fact]
    public void Compose_ScreenReaderTunedRaisesLiveRegionVerbosity()
    {
        var composite = CompositeProfile.Of(new DefaultProfile(), new ScreenReaderTunedProfile());

        composite.LiveRegions.Should().Be(LiveRegionVerbosity.Verbose);
    }

    [Fact]
    public void Compose_QuietProfileBeatsVerboseDoesNot_VerboseIsAdditive()
    {
        // Verbosity is "max wins" — opting into ScreenReaderTuned gives you Verbose,
        // even if another profile is Normal.
        var composite = CompositeProfile.Of(new ScreenReaderTunedProfile(), new LightProfile(), new HighContrastProfile());

        composite.LiveRegions.Should().Be(LiveRegionVerbosity.Verbose);
    }

    [Fact]
    public void Compose_ColorSchemeFollowsExplicitColorSchemeProfile()
    {
        // When a Light/Dark/HighContrast profile is in the mix, it picks the scheme.
        // Among multiple, the LAST one added wins — user-chosen order is meaningful.
        var composite = CompositeProfile.Of(new LightProfile(), new DarkProfile());
        composite.ColorScheme.Should().Be(ColorScheme.Dark);

        var composite2 = CompositeProfile.Of(new DarkProfile(), new HighContrastProfile());
        composite2.ColorScheme.Should().Be(ColorScheme.HighContrast);
    }

    [Fact]
    public void Compose_FormatDisplay_AppliesTheFirstTransformingProfile()
    {
        // Only one display formatter at a time makes sense; we use the first non-identity.
        var composite = CompositeProfile.Of(
            new NumberThousandsSeparatorsProfile(new CultureInfo("en-US")),
            new HighContrastProfile());

        composite.FormatDisplay("42000", "number").Should().Be("42,000");
    }

    [Fact]
    public void Compose_FormatDisplay_AllIdentityProfiles_PassesThrough()
    {
        var composite = CompositeProfile.Of(new LightProfile(), new HighContrastProfile());

        composite.FormatDisplay("42000", "number").Should().Be("42000");
    }

    [Fact]
    public void Compose_Name_ListsAllActiveProfilesForTelemetry()
    {
        var composite = CompositeProfile.Of(new HighContrastProfile(), new LargeTextProfile(), new ReducedMotionProfile());

        composite.Name.Should().Contain("HighContrast");
        composite.Name.Should().Contain("LargeText");
        composite.Name.Should().Contain("ReducedMotion");
    }

    [Fact]
    public void Compose_TouchTargetTakesMaxAcrossActiveProfiles()
    {
        // 36 floor + 56 from MotorAssist = 56 win. Also a non-square dimension override
        // would still take the max on each axis independently.
        var composite = CompositeProfile.Of(new DefaultProfile(), new LargeHitTargetsProfile(), new HighContrastProfile());

        composite.MinimumTouchTarget.Width.Should().Be(56);
        composite.MinimumTouchTarget.Height.Should().Be(56);
    }

    [Fact]
    public void Compose_UniversalCoreInvariant_RawTextThroughEveryComposition()
    {
        // Vision check: "five" in a number-hinted prompt survives every profile combo.
        var combos = new IRenderingProfile[][]
        {
            new IRenderingProfile[] { new NumberThousandsSeparatorsProfile(new CultureInfo("en-US")) },
            new IRenderingProfile[] { new NumberThousandsSeparatorsProfile(new CultureInfo("en-US")), new CurrencyDisplayProfile(new CultureInfo("en-US")), new HighContrastProfile(), new LargeTextProfile() },
            new IRenderingProfile[] { new LargeHitTargetsProfile(), new ScreenReaderTunedProfile(), new ReducedMotionProfile() },
            new IRenderingProfile[] { new DefaultProfile() },
        };

        foreach (var combo in combos)
        {
            var composite = CompositeProfile.Of(combo);
            composite.FormatDisplay("five", "number")
                .Should().Be("five", $"non-numeric text survives composition {composite.Name}");
            composite.FormatDisplay("n/a", "currency")
                .Should().Be("n/a");
        }
    }
}
