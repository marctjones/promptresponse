using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views.Prompts;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// End-to-end GUI verification of the input-mask auto-formatting: real keystrokes
/// flow into a real TextBox, the mask reshapes the visible text, and the bound VM
/// receives the reshaped string.
/// </summary>
public class InputMaskGuiTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static IProfileService NewServiceWithAllMasksEnabled()
    {
        var s = new ProfileService(new FixedProbe(), applyAffordanceDefaults: false);
        // Each input-mask formatter advertises its own gate flag now; the GUI tests
        // enable every mask flag so they exercise the live-reshape path uniformly.
        s.Enable<PhoneInputMaskProfile>();
        s.Enable<SsnInputMaskProfile>();
        s.Enable<EinInputMaskProfile>();
        s.Enable<ZipInputMaskProfile>();
        s.Enable<CurrencyInputMaskProfile>();
        s.Enable<PercentageInputMaskProfile>();
        return s;
    }

    private static IProfileService NewServiceWithoutAnyMasks()
        => new ProfileService(new FixedProbe(), applyAffordanceDefaults: false);

    private static Prompt P(string id, string label, string type, string response = "") =>
        new()
        {
            Id = id,
            Label = label,
            Response = response,
            Hints = new PromptHints { ExpectedDataType = type },
        };

    [AvaloniaFact]
    public void PhonePromptView_VisualFormattingOn_TypingDigits_ReshapesLive()
    {
        var service = NewServiceWithAllMasksEnabled();
        var vm = new PhonePromptViewModel(P("p", "Phone", "phone"), service);
        var view = new PhonePromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);

        var textBox = view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        textBox.Focus();
        GuiTestExtensions.PumpDispatcher();

        window.TypeText("5551234567");

        textBox.Text.Should().Be("(555) 123-4567",
            "VisualFormatting profile should reshape phone digits as the user types");
        vm.Response.Should().Be("(555) 123-4567",
            "the bound VM must see the reshaped value");
    }

    [AvaloniaFact]
    public void PhonePromptView_VisualFormattingOff_TypingDigits_StaysRaw()
    {
        // Universal core: no formatting profile means no reshape — invariant.
        var service = NewServiceWithoutAnyMasks();
        var vm = new PhonePromptViewModel(P("p", "Phone", "phone"), service);
        var view = new PhonePromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);

        var textBox = view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        textBox.Focus();
        GuiTestExtensions.PumpDispatcher();

        window.TypeText("5551234567");

        textBox.Text.Should().Be("5551234567",
            "without VisualFormatting, raw digits must stay raw");
        vm.Response.Should().Be("5551234567");
    }

    [AvaloniaFact]
    public void PhonePromptView_FreeText_PassesThroughUnchanged()
    {
        // Vision invariant: any visible text is a valid response. Typing "see HR"
        // into a phone-hinted prompt must NOT be reshaped.
        var service = NewServiceWithAllMasksEnabled();
        var vm = new PhonePromptViewModel(P("p", "Phone", "phone"), service);
        var view = new PhonePromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);

        var textBox = view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        textBox.Focus();
        GuiTestExtensions.PumpDispatcher();

        window.TypeText("see HR");

        textBox.Text.Should().Be("see HR");
        vm.Response.Should().Be("see HR");
    }

    [AvaloniaFact]
    public void TextPromptView_SsnHint_VisualFormattingOn_ReshapesLive()
    {
        // SSN hint is routed through TextPromptView (factory fallback). The view's
        // mask attachment should still find the right formatter via the registry.
        var service = NewServiceWithAllMasksEnabled();
        var vm = new TextPromptViewModel(P("p", "SSN", "ssn"), service);
        var view = new TextPromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);

        var textBox = view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        textBox.Focus();
        GuiTestExtensions.PumpDispatcher();

        window.TypeText("123456789");

        textBox.Text.Should().Be("123-45-6789");
        vm.Response.Should().Be("123-45-6789");
    }

    [AvaloniaFact]
    public void TextPromptView_EinHint_VisualFormattingOn_ReshapesLive()
    {
        var service = NewServiceWithAllMasksEnabled();
        var vm = new TextPromptViewModel(P("p", "EIN", "ein"), service);
        var view = new TextPromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);

        var textBox = view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        textBox.Focus();
        GuiTestExtensions.PumpDispatcher();

        window.TypeText("123456789");

        textBox.Text.Should().Be("12-3456789");
    }

    [AvaloniaFact]
    public void TextPromptView_ZipHint_VisualFormattingOn_ReshapesNinePlusFour()
    {
        var service = NewServiceWithAllMasksEnabled();
        var vm = new TextPromptViewModel(P("p", "ZIP", "zipcode"), service);
        var view = new TextPromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);

        var textBox = view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        textBox.Focus();
        GuiTestExtensions.PumpDispatcher();

        window.TypeText("902101234");

        textBox.Text.Should().Be("90210-1234");
    }

    [AvaloniaFact]
    public void TextPromptView_TextHint_NoMask_TypingPassesThrough()
    {
        // Plain text hint has no formatter — typing must NOT be reshaped.
        var service = NewServiceWithAllMasksEnabled();
        var vm = new TextPromptViewModel(P("p", "Name", "text"), service);
        var view = new TextPromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);

        var textBox = view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        textBox.Focus();
        GuiTestExtensions.PumpDispatcher();

        window.TypeText("123456789");

        textBox.Text.Should().Be("123456789");
    }
}
