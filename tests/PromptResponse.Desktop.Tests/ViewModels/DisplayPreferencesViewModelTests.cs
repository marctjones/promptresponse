using AwesomeAssertions;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels;
using System.Globalization;
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

    private static DisplayPreferencesViewModel CreateVm() => new(new ProfileService(new FixedProbe(), applyAffordanceDefaults: false));

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
        vm.NumberThousandsSeparators.Should().BeFalse();
        vm.CurrencyDisplay.Should().BeFalse();
        vm.IsoDatePrettify.Should().BeFalse();
        vm.DisplaysAsPreview.Should().BeFalse();
        vm.CalendarPicker.Should().BeFalse();
        vm.BooleanRadios.Should().BeFalse();
        vm.PhoneInputMask.Should().BeFalse();
        vm.SsnInputMask.Should().BeFalse();
        vm.EinInputMask.Should().BeFalse();
        vm.ZipInputMask.Should().BeFalse();
        vm.CurrencyInputMask.Should().BeFalse();
        vm.PercentageInputMask.Should().BeFalse();
        vm.LargeText.Should().BeFalse();
        vm.ReducedMotion.Should().BeFalse();
        vm.ScreenReaderTuned.Should().BeFalse();
        vm.LargeHitTargets.Should().BeFalse();
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
    public void ToggleNumberThousandsSeparators_RoundTripsThroughService()
    {
        var vm = CreateVm();

        vm.NumberThousandsSeparators = true;
        vm.NumberThousandsSeparators.Should().BeTrue();
        vm.ActiveProfile.FormatDisplay("42000", "number").Should().Be("42,000");

        vm.NumberThousandsSeparators = false;
        vm.NumberThousandsSeparators.Should().BeFalse();
        vm.ActiveProfile.FormatDisplay("42000", "number").Should().Be("42000");
    }

    [Fact]
    public void ToggleCurrencyDisplay_OnlyShapesCurrencyHint()
    {
        var vm = CreateVm();
        vm.CurrencyDisplay = true;

        vm.ActiveProfile.FormatDisplay("1234.56", "currency")
            .Should().Contain(CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol,
                "the currency display profile uses the active culture");
        vm.ActiveProfile.FormatDisplay("42000", "number").Should().Be("42000",
            "currency flag must not affect non-currency hints");
    }

    [Fact]
    public void ToggleIsoDatePrettify_OnlyShapesDateHint()
    {
        var vm = CreateVm();
        vm.IsoDatePrettify = true;

        vm.ActiveProfile.FormatDisplay("2026-04-29", "date").Should().NotBe("2026-04-29");
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
    public void ToggleLargeHitTargets_RaisesTouchTargetSize()
    {
        var vm = CreateVm();

        vm.LargeHitTargets = true;
        vm.ActiveProfile.MinimumTouchTarget.Width.Should().Be(56);

        vm.LargeHitTargets = false;
        vm.ActiveProfile.MinimumTouchTarget.Width.Should().Be(36);
    }

    [Fact]
    public void Reset_RestoresOsDetectedDefaults_OverridingUserToggles()
    {
        var probe = new FixedProbe { ReducedMotion = true };
        var vm = new DisplayPreferencesViewModel(new ProfileService(probe, applyAffordanceDefaults: false));
        vm.LargeHitTargets = true;
        vm.LargeText = true;

        vm.Reset();

        vm.LargeHitTargets.Should().BeFalse();
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
        vm.LargeHitTargets = true;
        vm.NumberThousandsSeparators = true;

        vm.ActiveProfile.TargetContrast.Should().Be(ContrastLevel.AAA);
        vm.ActiveProfile.TextScale.Should().Be(1.5);
        vm.ActiveProfile.AnimationsEnabled.Should().BeFalse();
        vm.ActiveProfile.MinimumTouchTarget.Width.Should().Be(56);
        vm.ActiveProfile.FormatDisplay("42000", "number").Should().Be("42,000");
    }
}
