using FluentAssertions;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// Unit tests for the Display Preferences view-model. Each toggle flips the
/// corresponding profile through the underlying IProfileService, and every change
/// raises PropertyChanged on every dependent property so the view re-binds correctly.
/// </summary>
public class DisplayPreferencesViewModelTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast { get; init; }
        public bool ReducedMotion { get; init; }
        public bool ScreenReaderActive { get; init; }
        public ColorScheme PreferredColorScheme { get; init; } = ColorScheme.Light;
    }

    private static DisplayPreferencesViewModel CreateVm() => new(new ProfileService(new FixedProbe()));

    [Fact]
    public void Constructor_RejectsNullService()
    {
        Action act = () => new DisplayPreferencesViewModel(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Defaults_ReflectLightColorSchemeAndNoEnhancements()
    {
        var vm = CreateVm();

        vm.IsLight.Should().BeTrue();
        vm.IsDark.Should().BeFalse();
        vm.IsHighContrast.Should().BeFalse();
        vm.VisualFormatting.Should().BeFalse();
        vm.LargeText.Should().BeFalse();
        vm.ReducedMotion.Should().BeFalse();
        vm.ScreenReaderTuned.Should().BeFalse();
        vm.MotorAssist.Should().BeFalse();
    }

    [Fact]
    public void SetIsDark_True_SwitchesColorScheme_AndRaisesPropertyChanged()
    {
        var vm = CreateVm();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.IsDark = true;

        vm.ColorScheme.Should().Be(ColorScheme.Dark);
        vm.IsDark.Should().BeTrue();
        vm.IsLight.Should().BeFalse();
        changed.Should().Contain(nameof(DisplayPreferencesViewModel.ColorScheme));
        changed.Should().Contain(nameof(DisplayPreferencesViewModel.IsDark));
        changed.Should().Contain(nameof(DisplayPreferencesViewModel.IsLight));
    }

    [Fact]
    public void SetIsHighContrast_True_PromotesToAaaContrast()
    {
        var vm = CreateVm();

        vm.IsHighContrast = true;

        vm.ActiveProfile.TargetContrast.Should().Be(ContrastLevel.AAA);
    }

    [Fact]
    public void SetIsLight_FalseWhenAlreadyLight_IsNoOp()
    {
        var vm = CreateVm();
        vm.IsLight.Should().BeTrue();

        vm.IsLight = false;  // Setter only acts on `value == true`.

        vm.IsLight.Should().BeTrue();
        vm.ColorScheme.Should().Be(ColorScheme.Light);
    }

    [Fact]
    public void ToggleVisualFormatting_RoundTripsThroughService()
    {
        var vm = CreateVm();

        vm.VisualFormatting = true;
        vm.VisualFormatting.Should().BeTrue();
        vm.ActiveProfile.FormatDisplay("42000", "number").Should().Be("42,000");

        vm.VisualFormatting = false;
        vm.VisualFormatting.Should().BeFalse();
        vm.ActiveProfile.FormatDisplay("42000", "number").Should().Be("42000");
    }

    [Fact]
    public void ToggleLargeText_AffectsTextScale()
    {
        var vm = CreateVm();

        vm.LargeText = true;
        vm.ActiveProfile.TextScale.Should().Be(1.5);

        vm.LargeText = false;
        vm.ActiveProfile.TextScale.Should().Be(1.0);
    }

    [Fact]
    public void ToggleReducedMotion_DisablesAnimations()
    {
        var vm = CreateVm();

        vm.ReducedMotion = true;
        vm.ActiveProfile.AnimationsEnabled.Should().BeFalse();

        vm.ReducedMotion = false;
        vm.ActiveProfile.AnimationsEnabled.Should().BeTrue();
    }

    [Fact]
    public void ToggleScreenReaderTuned_RaisesLiveRegionVerbosity()
    {
        var vm = CreateVm();

        vm.ScreenReaderTuned = true;
        vm.ActiveProfile.LiveRegions.Should().Be(LiveRegionVerbosity.Verbose);

        vm.ScreenReaderTuned = false;
        vm.ActiveProfile.LiveRegions.Should().Be(LiveRegionVerbosity.Normal);
    }

    [Fact]
    public void ToggleMotorAssist_RaisesTouchTargetSize()
    {
        var vm = CreateVm();

        vm.MotorAssist = true;
        vm.ActiveProfile.MinimumTouchTarget.Width.Should().Be(56);

        vm.MotorAssist = false;
        vm.ActiveProfile.MinimumTouchTarget.Width.Should().Be(36);
    }

    [Fact]
    public void Reset_RestoresOsDetectedDefaults_OverridingUserToggles()
    {
        var probe = new FixedProbe { ReducedMotion = true };
        var vm = new DisplayPreferencesViewModel(new ProfileService(probe));
        vm.MotorAssist = true;
        vm.LargeText = true;

        vm.Reset();

        vm.MotorAssist.Should().BeFalse();
        vm.LargeText.Should().BeFalse();
        vm.ReducedMotion.Should().BeTrue("OS-detected reduced-motion is restored after reset");
    }

    [Fact]
    public void TogglingTwiceWithSameValue_DoesNotRaisePropertyChanged_Repeatedly()
    {
        var vm = CreateVm();
        vm.LargeText = true;
        var changed = 0;
        vm.PropertyChanged += (_, _) => changed++;

        vm.LargeText = true;  // already true — no-op

        changed.Should().Be(0, "idempotent set must not raise PropertyChanged");
    }

    [Fact]
    public void DisplayPreferences_ComposesFullStack_ForMultiCapabilityUser()
    {
        // Vision: a user with multiple capability needs gets the union of
        // accommodations, not a single "best" pick.
        var vm = CreateVm();
        vm.IsHighContrast = true;
        vm.LargeText = true;
        vm.ReducedMotion = true;
        vm.MotorAssist = true;
        vm.VisualFormatting = true;

        vm.ActiveProfile.TargetContrast.Should().Be(ContrastLevel.AAA);
        vm.ActiveProfile.TextScale.Should().Be(1.5);
        vm.ActiveProfile.AnimationsEnabled.Should().BeFalse();
        vm.ActiveProfile.MinimumTouchTarget.Width.Should().Be(56);
        vm.ActiveProfile.FormatDisplay("42000", "number").Should().Be("42,000");
    }
}
