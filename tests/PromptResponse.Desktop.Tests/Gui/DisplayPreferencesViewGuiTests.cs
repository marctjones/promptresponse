using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using FluentAssertions;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.Views;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// End-to-end GUI tests for the Display Preferences view: real keyboard and mouse
/// events propagate through the visual tree, flip toggles, and update the underlying
/// IProfileService. Vision-critical because this is the surface every user touches
/// when configuring their capability profile.
/// </summary>
public class DisplayPreferencesViewGuiTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast { get; init; }
        public bool ReducedMotion { get; init; }
        public bool ScreenReaderActive { get; init; }
        public ColorScheme PreferredColorScheme { get; init; } = ColorScheme.Light;
    }

    private static (DisplayPreferencesView view, DisplayPreferencesViewModel vm, IProfileService service) BuildView()
    {
        var service = new ProfileService(new FixedProbe(), applyAffordanceDefaults: false);
        var vm = new DisplayPreferencesViewModel(service);
        var view = new DisplayPreferencesView { DataContext = vm };
        return (view, vm, service);
    }

    [AvaloniaFact]
    public void View_LoadsWithDefaults_LightSchemeIsCheckedAndAccessible()
    {
        var (view, _, _) = BuildView();
        var window = view.ShowInWindow(width: 700, height: 720);
        window.ExpandExpander(view.FindDescendant<Expander>(e => true));

        var lightRadio = view.FindDescendant<RadioButton>(rb => rb.Name == "LightRadio");

        lightRadio.IsChecked.Should().BeTrue("Light is the default capability profile");
        lightRadio.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)
            .Should().Be("Light color scheme",
                "the AutomationProperties.Name must announce the radio's purpose to screen readers");
    }

    [AvaloniaFact]
    public void SelectingDarkRadio_ViaActivation_UpdatesActiveProfile()
    {
        var (view, vm, _) = BuildView();
        var window = view.ShowInWindow(width: 700, height: 720);

        // The radio is inside the Customize Expander — must expand it first
        // (real users click the expander chevron; headless can't, so the harness
        // handles the fallback).
        window.ExpandExpander(view.FindDescendant<Expander>(e => true));
        window.Activate(view.FindDescendant<RadioButton>(rb => rb.Name == "DarkRadio"));

        vm.ColorScheme.Should().Be(ColorScheme.Dark);
        vm.ActiveProfile.ColorScheme.Should().Be(ColorScheme.Dark);
    }

    [AvaloniaFact]
    public void SelectingHighContrastRadio_PromotesContrastToAaa()
    {
        var (view, vm, _) = BuildView();
        var window = view.ShowInWindow(width: 700, height: 720);

        window.ExpandExpander(view.FindDescendant<Expander>(e => true));
        window.Activate(view.FindDescendant<RadioButton>(rb => rb.Name == "HighContrastRadio"));

        vm.ActiveProfile.TargetContrast.Should().Be(ContrastLevel.AAA);
    }

    [AvaloniaFact]
    public void EveryFlagCheckbox_IsAccessible_ForScreenReaderUsers()
    {
        // Every customize-panel flag has an x:Name and an AutomationProperties.Name.
        // This guards against a regression where adding a new flag forgets one.
        var (view, _, _) = BuildView();
        var window = view.ShowInWindow(width: 700, height: 720);
        window.ExpandExpander(view.FindDescendant<Expander>(e => true));

        var checkboxNames = new[]
        {
            "LargeTextCheck", "ReducedMotionCheck", "ScreenReaderTunedCheck", "LargeHitTargetsCheck",
            "NumberThousandsSeparatorsCheck", "CurrencyDisplayCheck", "IsoDatePrettifyCheck", "DisplaysAsPreviewCheck",
            "CalendarPickerCheck", "BooleanRadiosCheck",
            "PhoneInputMaskCheck", "SsnInputMaskCheck", "EinInputMaskCheck", "ZipInputMaskCheck",
            "CurrencyInputMaskCheck", "PercentageInputMaskCheck",
        };

        foreach (var name in checkboxNames)
        {
            var control = view.FindDescendant<CheckBox>(c => c.Name == name);
            control.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)
                .Should().NotBeNullOrWhiteSpace(
                    $"every flag checkbox must announce itself to screen readers; '{name}' is missing AutomationProperties.Name");
        }
    }

    [AvaloniaFact]
    public void CheckBox_BoundToVm_PropagatesChangesBothWays()
    {
        var (view, vm, _) = BuildView();
        var window = view.ShowInWindow(width: 700, height: 720);

        window.ExpandExpander(view.FindDescendant<Expander>(e => true));
        var checkbox = view.FindDescendant<CheckBox>(cb => cb.Name == "LargeTextCheck");
        window.Activate(checkbox);

        vm.LargeText.Should().BeTrue();
        vm.ActiveProfile.TextScale.Should().Be(1.5);

        // Reverse direction: VM update reflects on the visible CheckBox.
        vm.LargeText = false;
        GuiTestExtensions.PumpDispatcher();
        checkbox.IsChecked.Should().BeFalse();
    }

    [AvaloniaFact]
    public void TogglingMultipleCheckboxes_ComposesTheActiveProfile()
    {
        var (view, vm, _) = BuildView();
        var window = view.ShowInWindow(width: 700, height: 720);

        window.ExpandExpander(view.FindDescendant<Expander>(e => true));
        window.Activate(view.FindDescendant<CheckBox>(cb => cb.Name == "NumberThousandsSeparatorsCheck"));
        window.Activate(view.FindDescendant<CheckBox>(cb => cb.Name == "LargeTextCheck"));
        window.Activate(view.FindDescendant<CheckBox>(cb => cb.Name == "LargeHitTargetsCheck"));

        vm.ActiveProfile.TextScale.Should().Be(1.5);
        vm.ActiveProfile.MinimumTouchTarget.Width.Should().Be(56);
        vm.ActiveProfile.FormatDisplay("42000", "number").Should().Be("42,000");
    }

    [AvaloniaFact]
    public void ResetButton_ViaRealKeyboard_ClearsUserToggles()
    {
        var (view, vm, _) = BuildView();
        var window = view.ShowInWindow(width: 700, height: 720);

        window.ExpandExpander(view.FindDescendant<Expander>(e => true));
        window.Activate(view.FindDescendant<CheckBox>(cb => cb.Name == "LargeTextCheck"));
        vm.LargeText.Should().BeTrue();

        var resetButton = view.FindDescendant<Button>(b => b.Name == "ResetButton");
        window.Activate(resetButton);

        vm.LargeText.Should().BeFalse("Reset button activation must clear user toggles");
    }

    [AvaloniaFact]
    public void PresetButtons_ApplyTheirCompositions_ViaRealKeyboardEnter()
    {
        var (view, vm, _) = BuildView();
        var window = view.ShowInWindow(width: 700, height: 720);

        window.Activate(view.FindDescendant<Button>(b => b.Name == "PresetExcellent"));
        vm.ColorScheme.Should().Be(ColorScheme.Light);
        vm.NumberThousandsSeparators.Should().BeTrue();
        vm.PhoneInputMask.Should().BeTrue();
        vm.CalendarPicker.Should().BeTrue();

        window.Activate(view.FindDescendant<Button>(b => b.Name == "PresetLowVision"));
        vm.ColorScheme.Should().Be(ColorScheme.HighContrast);
        vm.LargeText.Should().BeTrue();
        vm.LargeHitTargets.Should().BeTrue();

        window.Activate(view.FindDescendant<Button>(b => b.Name == "PresetBlind"));
        vm.ScreenReaderTuned.Should().BeTrue();
        vm.PhoneInputMask.Should().BeFalse("live phone reshape disrupts screen-reader speech");
        vm.SsnInputMask.Should().BeFalse();
        vm.CalendarPicker.Should().BeFalse("calendar picker grid is slower than typing ISO date");
        vm.BooleanRadios.Should().BeTrue("yes/no radios are arrow-key friendly");
        vm.CurrencyInputMask.Should().BeTrue("commit-time currency mask fires once on LostFocus");
    }

    [AvaloniaFact]
    public void PresetButtons_HaveAccessibleNames()
    {
        var (view, _, _) = BuildView();
        view.ShowInWindow(width: 700, height: 720);

        var presetNames = new[]
        {
            "PresetExcellent", "PresetBlind", "PresetLowVision", "PresetCognitive", "PresetMotor",
        };
        foreach (var name in presetNames)
        {
            var btn = view.FindDescendant<Button>(b => b.Name == name);
            btn.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)
                .Should().NotBeNullOrWhiteSpace($"preset '{name}' must announce itself to screen readers");
        }
    }
}
