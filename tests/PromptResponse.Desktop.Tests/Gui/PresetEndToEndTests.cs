using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views;
using PromptResponse.Desktop.Views.Prompts;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// End-to-end preset flow tests using ONLY synthesized real input
/// (mouse clicks, key presses) — no programmatic property mutation. Each test
/// follows the path a live user takes:
///   1) Open Display Preferences
///   2) Expand the Customize panel via real click
///   3) Click a preset button via real mouse click
///   4) Verify the customize-panel checkbox states reflect the preset
///   5) Open a prompt view in a separate window
///   6) Verify the prompt's affordances (calendar picker / radios / preview)
///      respect the preset
/// Bugs surface as test failures here when the headless-only suite stays green
/// because the live input pipeline (focus, hit-testing, binding-update timing)
/// fails outside of programmatic mutation.
/// </summary>
public class PresetEndToEndTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast { get; init; }
        public bool ReducedMotion { get; init; }
        public bool ScreenReaderActive { get; init; }
        public ColorScheme PreferredColorScheme { get; init; } = ColorScheme.Light;
    }

    private static (DisplayPreferencesView view, DisplayPreferencesViewModel vm, IProfileService service, Window window) BuildPrefs()
    {
        var service = new ProfileService(new FixedProbe(), applyAffordanceDefaults: false);
        var vm = new DisplayPreferencesViewModel(service);
        var view = new DisplayPreferencesView { DataContext = vm };
        var window = view.ShowInWindow(width: 800, height: 900);
        return (view, vm, service, window);
    }

    private static Prompt P(string id, string label, string? type = null, string response = "") =>
        new()
        {
            Id = id,
            Label = label,
            Response = response,
            Hints = new PromptHints { ExpectedDataType = type },
        };

    [AvaloniaFact]
    public void ExcellentVisionPreset_RealClick_TurnsEveryAffordanceOn()
    {
        var (view, vm, service, window) = BuildPrefs();

        var presetBtn = view.FindDescendant<Button>(b => b.Name == "PresetExcellent");
        window.Activate(presetBtn);

        // After preset apply, every per-flag bool should be true.
        vm.PhoneInputMask.Should().BeTrue();
        vm.SsnInputMask.Should().BeTrue();
        vm.EinInputMask.Should().BeTrue();
        vm.ZipInputMask.Should().BeTrue();
        vm.CurrencyInputMask.Should().BeTrue();
        vm.PercentageInputMask.Should().BeTrue();
        vm.NumberThousandsSeparators.Should().BeTrue();
        vm.CurrencyDisplay.Should().BeTrue();
        vm.IsoDatePrettify.Should().BeTrue();
        vm.DisplaysAsPreview.Should().BeTrue();
        vm.CalendarPicker.Should().BeTrue();
        vm.BooleanRadios.Should().BeTrue();
        vm.ColorScheme.Should().Be(ColorScheme.Light);
    }

    [AvaloniaFact]
    public void ExcellentVisionPreset_PropagatesToCustomizePanelCheckboxes()
    {
        // The customize-panel checkboxes are TwoWay-bound to the same VM bools the
        // preset flips. After the preset, those checkboxes must reflect the new state.
        var (view, _, _, window) = BuildPrefs();

        // Expand customize so the checkboxes are in the visual tree.
        var customizeExpander = view.FindDescendant<Expander>(e => true);
        window.ExpandExpander(customizeExpander);

        // Click the preset.
        window.Activate(view.FindDescendant<Button>(b => b.Name == "PresetExcellent"));

        // Each customize-panel checkbox should now be checked.
        var checkboxes = new[]
        {
            "PhoneInputMaskCheck", "SsnInputMaskCheck", "EinInputMaskCheck",
            "ZipInputMaskCheck", "CurrencyInputMaskCheck", "PercentageInputMaskCheck",
            "NumberThousandsSeparatorsCheck", "CurrencyDisplayCheck", "IsoDatePrettifyCheck",
            "DisplaysAsPreviewCheck", "CalendarPickerCheck", "BooleanRadiosCheck",
        };
        foreach (var name in checkboxes)
        {
            view.FindDescendant<CheckBox>(c => c.Name == name)
                .IsChecked.Should().BeTrue(
                    $"preset apply must propagate to '{name}' via the bound VM property");
        }
    }

    [AvaloniaFact]
    public void BlindPreset_DisablesLiveInputMasks_KeepsCommitMasks()
    {
        var (view, vm, _, window) = BuildPrefs();

        window.Activate(view.FindDescendant<Button>(b => b.Name == "PresetBlind"));

        // Live masks OFF — they interrupt screen-reader speech.
        vm.PhoneInputMask.Should().BeFalse();
        vm.SsnInputMask.Should().BeFalse();
        vm.EinInputMask.Should().BeFalse();
        vm.ZipInputMask.Should().BeFalse();
        // Commit masks ON — single announcement on LostFocus, low disruption.
        vm.CurrencyInputMask.Should().BeTrue();
        vm.PercentageInputMask.Should().BeTrue();
        // Calendar picker OFF — typed ISO date is faster.
        vm.CalendarPicker.Should().BeFalse();
        // Radios ON — arrow-key friendly.
        vm.BooleanRadios.Should().BeTrue();
        // Display previews ON — confirm intent.
        vm.NumberThousandsSeparators.Should().BeTrue();
        vm.CurrencyDisplay.Should().BeTrue();
        vm.IsoDatePrettify.Should().BeTrue();
        vm.DisplaysAsPreview.Should().BeTrue();
        // Screen-reader tuned ON.
        vm.ScreenReaderTuned.Should().BeTrue();
    }

    [AvaloniaFact]
    public void LowVisionPreset_AppliesHighContrastAndLargeText()
    {
        var (view, vm, _, window) = BuildPrefs();

        window.Activate(view.FindDescendant<Button>(b => b.Name == "PresetLowVision"));

        vm.ColorScheme.Should().Be(ColorScheme.HighContrast);
        vm.LargeText.Should().BeTrue();
        vm.LargeHitTargets.Should().BeTrue();
        // Affordances all on — visual reshaping aids reduced acuity.
        vm.PhoneInputMask.Should().BeTrue();
        vm.CalendarPicker.Should().BeTrue();
        vm.NumberThousandsSeparators.Should().BeTrue();
    }

    [AvaloniaFact]
    public void CalendarPickerCheckbox_RealKeyboardSpace_TogglesPickerVisibilityInDateView()
    {
        // Cross-window E2E: toggle the flag in Display Preferences via real keyboard,
        // then open a Date prompt and verify the picker IsVisible state reflects it.
        var (prefsView, vm, service, prefsWindow) = BuildPrefs();
        var customize = prefsView.FindDescendant<Expander>(e => true);
        prefsWindow.ExpandExpander(customize);

        var calCheckbox = prefsView.FindDescendant<CheckBox>(c => c.Name == "CalendarPickerCheck");
        calCheckbox.IsChecked.Should().BeFalse("default profile has no flags");

        // Open a date prompt sharing the same profile service.
        var dateVm = new DatePromptViewModel(P("d1", "DOB", "date"), service);
        var dateView = new DatePromptView { DataContext = dateVm };
        var dateWindow = dateView.ShowInWindow(width: 600, height: 200);
        var picker = dateView.FindDescendant<CalendarDatePicker>(p => p.Name == "DatePicker");

        picker.IsVisible.Should().BeFalse("universal core hides the picker by default");

        // Toggle via real keyboard Space.
        prefsWindow.Activate(calCheckbox);

        calCheckbox.IsChecked.Should().BeTrue();
        vm.CalendarPicker.Should().BeTrue();
        // Pump dispatcher so the date VM's binding refreshes.
        GuiTestExtensions.PumpDispatcher();
        picker.IsVisible.Should().BeTrue("flag toggle must propagate to bound IsVisible");
    }

    [AvaloniaFact]
    public void BooleanRadiosCheckbox_RealMouseClick_TogglesRadioVisibilityInBooleanView()
    {
        var (prefsView, vm, service, prefsWindow) = BuildPrefs();
        prefsWindow.ExpandExpander(prefsView.FindDescendant<Expander>(e => true));

        var boolVm = new BooleanPromptViewModel(P("b1", "Resident", "boolean"), service);
        var boolView = new BooleanPromptView { DataContext = boolVm };
        boolView.ShowInWindow(width: 600, height: 200);
        var yesRadio = boolView.FindDescendant<RadioButton>(r => r.Name == "YesRadio");

        yesRadio.IsVisible.Should().BeFalse("universal core hides the radios by default");

        var checkbox = prefsView.FindDescendant<CheckBox>(c => c.Name == "BooleanRadiosCheck");
        prefsWindow.Activate(checkbox);

        checkbox.IsChecked.Should().BeTrue();
        vm.BooleanRadios.Should().BeTrue();
        GuiTestExtensions.PumpDispatcher();
        yesRadio.IsVisible.Should().BeTrue();
    }

    [AvaloniaFact]
    public void PhoneInputMaskCheckbox_RealClick_GatesLiveReshape()
    {
        var (prefsView, vm, service, prefsWindow) = BuildPrefs();
        prefsWindow.ExpandExpander(prefsView.FindDescendant<Expander>(e => true));

        var phoneVm = new PhonePromptViewModel(P("p1", "Phone", "phone"), service);
        var phoneView = new PhonePromptView { DataContext = phoneVm };
        var phoneWindow = phoneView.ShowInWindow(width: 600, height: 200);
        var textBox = phoneView.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");

        // With the flag off, typing digits must NOT reshape — universal core.
        textBox.Focus();
        GuiTestExtensions.PumpDispatcher();
        phoneWindow.TypeText("5551234567");
        textBox.Text.Should().Be("5551234567",
            "without PhoneInputMask flag, raw digits stay raw");

        // Now enable the flag via the customize-panel checkbox.
        prefsWindow.Activate(prefsView.FindDescendant<CheckBox>(c => c.Name == "PhoneInputMaskCheck"));
        vm.PhoneInputMask.Should().BeTrue();

        // Clear and re-type — should reshape now.
        textBox.Text = string.Empty;
        textBox.Focus();
        GuiTestExtensions.PumpDispatcher();
        phoneWindow.TypeText("5551234567");
        textBox.Text.Should().Be("(555) 123-4567",
            "after toggling PhoneInputMask via real click, digits must reshape live");
    }
}
