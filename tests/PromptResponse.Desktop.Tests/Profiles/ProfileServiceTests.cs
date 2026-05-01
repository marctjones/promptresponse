using FluentAssertions;
using PromptResponse.Desktop.Profiles;
using Xunit;

namespace PromptResponse.Desktop.Tests.Profiles;

/// <summary>
/// Tests the IProfileService contract: tracks active profiles, persists user choice,
/// auto-detects OS preferences, and emits ProfileChanged events on every transition.
/// </summary>
public class ProfileServiceTests
{
    private static IOsAccessibilityProbe NoPreferences() => new StubProbe();

    private sealed class StubProbe : IOsAccessibilityProbe
    {
        public bool HighContrast { get; init; }
        public bool ReducedMotion { get; init; }
        public bool ScreenReaderActive { get; init; }
        public ColorScheme PreferredColorScheme { get; init; } = ColorScheme.Light;
    }

    [Fact]
    public void NewService_NoOsPreferences_ActiveProfileIsLight()
    {
        var service = new ProfileService(NoPreferences());

        service.ActiveProfile.ColorScheme.Should().Be(ColorScheme.Light);
        service.IsActive(typeof(LightProfile)).Should().BeTrue();
    }

    [Fact]
    public void Constructor_DetectsOsHighContrast_AutoEnablesHighContrastProfile()
    {
        var probe = new StubProbe { HighContrast = true, PreferredColorScheme = ColorScheme.HighContrast };

        var service = new ProfileService(probe);

        service.IsActive(typeof(HighContrastProfile)).Should().BeTrue();
        service.ActiveProfile.TargetContrast.Should().Be(ContrastLevel.AAA);
    }

    [Fact]
    public void Constructor_DetectsOsReducedMotion_AutoEnablesReducedMotionProfile()
    {
        var probe = new StubProbe { ReducedMotion = true };

        var service = new ProfileService(probe);

        service.IsActive(typeof(ReducedMotionProfile)).Should().BeTrue();
        service.ActiveProfile.AnimationsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Constructor_DetectsOsScreenReader_AutoEnablesScreenReaderTuned()
    {
        var probe = new StubProbe { ScreenReaderActive = true };

        var service = new ProfileService(probe);

        service.IsActive(typeof(ScreenReaderTunedProfile)).Should().BeTrue();
        service.ActiveProfile.LiveRegions.Should().Be(LiveRegionVerbosity.Verbose);
    }

    [Fact]
    public void Constructor_DetectsOsDark_SwitchesToDarkProfile()
    {
        var probe = new StubProbe { PreferredColorScheme = ColorScheme.Dark };

        var service = new ProfileService(probe);

        service.IsActive(typeof(DarkProfile)).Should().BeTrue();
        service.IsActive(typeof(LightProfile)).Should().BeFalse();
        service.ActiveProfile.ColorScheme.Should().Be(ColorScheme.Dark);
    }

    [Fact]
    public void Enable_AddsProfileToActiveSet_AndRaisesProfileChanged()
    {
        var service = new ProfileService(NoPreferences());
        var raised = 0;
        service.ProfileChanged += (_, _) => raised++;

        service.Enable<LargeTextProfile>();

        service.IsActive(typeof(LargeTextProfile)).Should().BeTrue();
        service.ActiveProfile.TextScale.Should().Be(1.5);
        raised.Should().Be(1);
    }

    [Fact]
    public void Enable_SameProfileTwice_IsIdempotent_AndDoesNotRaiseEventTwice()
    {
        var service = new ProfileService(NoPreferences());
        service.Enable<LargeTextProfile>();
        var raised = 0;
        service.ProfileChanged += (_, _) => raised++;

        service.Enable<LargeTextProfile>();

        raised.Should().Be(0, "no actual change happened");
    }

    [Fact]
    public void Disable_RemovesProfile_AndRaisesProfileChanged()
    {
        var service = new ProfileService(NoPreferences());
        service.Enable<LargeTextProfile>();
        var raised = 0;
        service.ProfileChanged += (_, _) => raised++;

        service.Disable<LargeTextProfile>();

        service.IsActive(typeof(LargeTextProfile)).Should().BeFalse();
        service.ActiveProfile.TextScale.Should().Be(1.0);
        raised.Should().Be(1);
    }

    [Fact]
    public void Disable_NotEnabledProfile_IsSafe()
    {
        var service = new ProfileService(NoPreferences());
        var raised = 0;
        service.ProfileChanged += (_, _) => raised++;

        service.Disable<LargeHitTargetsProfile>();

        raised.Should().Be(0);
        service.IsActive(typeof(LargeHitTargetsProfile)).Should().BeFalse();
    }

    [Fact]
    public void SetColorScheme_SwapsLightForDark_RaisingExactlyOnce()
    {
        var service = new ProfileService(NoPreferences());
        var raised = 0;
        service.ProfileChanged += (_, _) => raised++;

        service.SetColorScheme(ColorScheme.Dark);

        service.ActiveProfile.ColorScheme.Should().Be(ColorScheme.Dark);
        service.IsActive(typeof(LightProfile)).Should().BeFalse();
        service.IsActive(typeof(DarkProfile)).Should().BeTrue();
        raised.Should().Be(1);
    }

    [Fact]
    public void SetColorScheme_HighContrast_AlsoPromotesContrastBudget()
    {
        var service = new ProfileService(NoPreferences());

        service.SetColorScheme(ColorScheme.HighContrast);

        service.ActiveProfile.TargetContrast.Should().Be(ContrastLevel.AAA);
    }

    [Fact]
    public void Compose_AccessibilityStack_AllEnhancementsCoExist()
    {
        var service = new ProfileService(NoPreferences());
        service.SetColorScheme(ColorScheme.HighContrast);
        service.Enable<LargeTextProfile>();
        service.Enable<ReducedMotionProfile>();
        service.Enable<LargeHitTargetsProfile>();
        service.Enable<ScreenReaderTunedProfile>();
        service.Enable<NumberThousandsSeparatorsProfile>();
        service.Enable<CurrencyDisplayProfile>();
        service.Enable<IsoDatePrettifyProfile>();

        service.ActiveProfile.ColorScheme.Should().Be(ColorScheme.HighContrast);
        service.ActiveProfile.TargetContrast.Should().Be(ContrastLevel.AAA);
        service.ActiveProfile.TextScale.Should().Be(1.5);
        service.ActiveProfile.AnimationsEnabled.Should().BeFalse();
        service.ActiveProfile.MinimumTouchTarget.Width.Should().Be(56);
        service.ActiveProfile.LiveRegions.Should().Be(LiveRegionVerbosity.Verbose);
        service.ActiveProfile.FormatDisplay("42000", "number").Should().Be("42,000");
    }

    [Fact]
    public void Reset_ClearsAllUserChoices_AndRestoresOsDefaults()
    {
        var probe = new StubProbe { ReducedMotion = true };
        var service = new ProfileService(probe);
        service.Enable<LargeTextProfile>();
        service.Enable<LargeHitTargetsProfile>();

        service.Reset();

        service.IsActive(typeof(LargeTextProfile)).Should().BeFalse();
        service.IsActive(typeof(LargeHitTargetsProfile)).Should().BeFalse();
        service.IsActive(typeof(ReducedMotionProfile)).Should().BeTrue("OS preferences are still respected after reset");
    }
}
