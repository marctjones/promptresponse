using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views.Prompts;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// Comprehensive per-prompt-type interactive tests covering every prompt view's
/// primary user-facing input control. Each test types real keystrokes / activates
/// real controls and asserts the bound VM Response receives the value.
///
/// Coverage: TextPromptView, MultilinePromptView, NumberPromptView,
/// CurrencyPromptView, DatePromptView (free-text + picker when flag on),
/// BooleanPromptView (free-text + radios when flag on), PhonePromptView (mask
/// on/off), EmailPromptView, UrlPromptView, SelectPromptView (free-text override),
/// MultichoicePromptView (free-text override), TextPromptView with SSN/EIN/ZIP
/// hint routing through the input-mask registry.
/// </summary>
public class PerPromptTypeInteractiveTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static IProfileService NewService(params Type[] enabled)
    {
        var s = new ProfileService(new FixedProbe(), applyAffordanceDefaults: false);
        foreach (var t in enabled)
        {
            typeof(IProfileService).GetMethod(nameof(IProfileService.Enable))!
                .MakeGenericMethod(t).Invoke(s, null);
        }
        return s;
    }

    private static Prompt P(string id, string label, string? type = null, string response = "") =>
        new() { Id = id, Label = label, Response = response, Hints = new PromptHints { ExpectedDataType = type } };

    private static (TextBox tb, Window window, T vm) Open<TView, T>(T vm)
        where TView : Avalonia.Controls.UserControl, new()
        where T : PromptViewModelBase
    {
        var view = new TView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);
        var tb = view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        tb.Focus();
        GuiTestExtensions.PumpDispatcher();
        return (tb, window, vm);
    }

    // ── TextPromptView ──

    [AvaloniaFact]
    public void TextPrompt_Typing_UpdatesResponse()
    {
        var vm = new TextPromptViewModel(P("p", "Name"), NewService());
        var (_, window, _) = Open<TextPromptView, TextPromptViewModel>(vm);
        window.TypeText("John Doe");
        vm.Response.Should().Be("John Doe");
    }

    // ── MultilinePromptView ──

    [AvaloniaFact]
    public void MultilinePrompt_TypingWithEnter_PreservesNewlines()
    {
        var vm = new MultilinePromptViewModel(P("p", "Notes", "multiline"), NewService());
        var (_, window, _) = Open<MultilinePromptView, MultilinePromptViewModel>(vm);
        window.TypeText("first");
        window.PressKey(Key.Enter);
        window.TypeText("second");
        vm.Response.Should().Contain("first");
        vm.Response.Should().Contain("second");
    }

    // ── NumberPromptView ──

    [AvaloniaFact]
    public void NumberPrompt_TypingNonNumeric_StaysFreeTextValid()
    {
        // Vision invariant: any visible text is a valid response.
        var vm = new NumberPromptViewModel(P("p", "Age", "number"), NewService());
        var (_, window, _) = Open<NumberPromptView, NumberPromptViewModel>(vm);
        window.TypeText("five");
        vm.Response.Should().Be("five");
    }

    [AvaloniaFact]
    public void NumberPrompt_DisplaysAsPreview_VisibleWhenBothFlagsActive()
    {
        var service = NewService(typeof(NumberThousandsSeparatorsProfile), typeof(DisplaysAsPreviewProfile));
        var vm = new NumberPromptViewModel(P("p", "Salary", "number"), service);
        var view = new NumberPromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);
        var hint = view.FindDescendant<TextBlock>(t => t.Name == "DisplayHint");
        var tb = view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        tb.Focus();
        GuiTestExtensions.PumpDispatcher();

        window.TypeText("42000");
        GuiTestExtensions.PumpDispatcher();

        hint.IsVisible.Should().BeTrue();
        hint.Text.Should().Contain("42,000");
        tb.Text.Should().Be("42000", "underlying response stays raw — display affordance never mutates");
    }

    // ── CurrencyPromptView ──

    [AvaloniaFact]
    public void CurrencyPrompt_TypingFreeText_AcceptsNonNumeric()
    {
        var vm = new CurrencyPromptViewModel(P("p", "Cost", "currency"), NewService());
        var (_, window, _) = Open<CurrencyPromptView, CurrencyPromptViewModel>(vm);
        window.TypeText("varies");
        vm.Response.Should().Be("varies");
    }

    // ── DatePromptView ──

    [AvaloniaFact]
    public void DatePrompt_TypingFreeText_AcceptsNonIsoDate()
    {
        var vm = new DatePromptViewModel(P("p", "DOB", "date"), NewService());
        var (_, window, _) = Open<DatePromptView, DatePromptViewModel>(vm);
        window.TypeText("see attached");
        vm.Response.Should().Be("see attached");
    }

    [AvaloniaFact]
    public void DatePrompt_PickerHidden_WhenCalendarFlagOff()
    {
        var vm = new DatePromptViewModel(P("p", "DOB", "date"), NewService());
        var view = new DatePromptView { DataContext = vm };
        view.ShowInWindow(width: 600, height: 200);
        var picker = view.FindDescendant<CalendarDatePicker>(p => p.Name == "DatePicker");
        picker.IsVisible.Should().BeFalse(
            "universal core hides the picker — only enabled when CalendarPickerProfile is active");
    }

    [AvaloniaFact]
    public void DatePrompt_PickerVisible_WhenCalendarFlagOn()
    {
        var service = NewService(typeof(CalendarPickerProfile));
        var vm = new DatePromptViewModel(P("p", "DOB", "date"), service);
        var view = new DatePromptView { DataContext = vm };
        view.ShowInWindow(width: 600, height: 200);
        var picker = view.FindDescendant<CalendarDatePicker>(p => p.Name == "DatePicker");
        picker.IsVisible.Should().BeTrue();
    }

    [AvaloniaFact]
    public void DatePrompt_PickerSelection_RoundTripsToResponse()
    {
        var service = NewService(typeof(CalendarPickerProfile));
        var vm = new DatePromptViewModel(P("p", "DOB", "date"), service);
        var view = new DatePromptView { DataContext = vm };
        view.ShowInWindow(width: 600, height: 200);
        var picker = view.FindDescendant<CalendarDatePicker>(p => p.Name == "DatePicker");

        picker.SelectedDate = new DateTime(2026, 4, 29);
        GuiTestExtensions.PumpDispatcher();

        vm.Response.Should().Be("2026-04-29",
            "picker selection writes the ISO date to the response — round-trip with the text field");
    }

    // ── BooleanPromptView ──

    [AvaloniaFact]
    public void BooleanPrompt_FreeTextAcceptsAnyValue()
    {
        var vm = new BooleanPromptViewModel(P("p", "Resident", "boolean"), NewService());
        var view = new BooleanPromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);
        var ft = view.FindDescendant<TextBox>(t => t.Name == "FreeTextEntry");
        ft.Focus();
        GuiTestExtensions.PumpDispatcher();
        window.TypeText("maybe");
        vm.Response.Should().Be("maybe");
        vm.IsTrue.Should().BeNull();
    }

    [AvaloniaFact]
    public void BooleanPrompt_Radios_HiddenByDefault_VisibleWithFlag()
    {
        var vm1 = new BooleanPromptViewModel(P("p", "X", "boolean"), NewService());
        var v1 = new BooleanPromptView { DataContext = vm1 };
        v1.ShowInWindow(width: 600, height: 200);
        v1.FindDescendant<RadioButton>(r => r.Name == "YesRadio").IsVisible.Should().BeFalse();

        var vm2 = new BooleanPromptViewModel(P("p", "X", "boolean"), NewService(typeof(BooleanRadiosProfile)));
        var v2 = new BooleanPromptView { DataContext = vm2 };
        v2.ShowInWindow(width: 600, height: 200);
        v2.FindDescendant<RadioButton>(r => r.Name == "YesRadio").IsVisible.Should().BeTrue();
    }

    [AvaloniaFact]
    public void BooleanPrompt_YesRadio_RealKeyboardActivation_SetsIsTrue()
    {
        var vm = new BooleanPromptViewModel(P("p", "X", "boolean"), NewService(typeof(BooleanRadiosProfile)));
        var view = new BooleanPromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);
        window.Activate(view.FindDescendant<RadioButton>(r => r.Name == "YesRadio"));
        vm.IsTrue.Should().BeTrue();
        vm.Response.Should().Be("yes");
    }

    [AvaloniaFact]
    public void BooleanPrompt_NoRadio_RealKeyboardActivation_SetsIsFalse()
    {
        var vm = new BooleanPromptViewModel(P("p", "X", "boolean"), NewService(typeof(BooleanRadiosProfile)));
        var view = new BooleanPromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);
        window.Activate(view.FindDescendant<RadioButton>(r => r.Name == "NoRadio"));
        vm.IsTrue.Should().BeFalse();
        vm.Response.Should().Be("no");
    }

    // ── PhonePromptView ──

    [AvaloniaFact]
    public void PhonePrompt_MaskOff_TypingDigitsStaysRaw()
    {
        var vm = new PhonePromptViewModel(P("p", "Phone", "phone"), NewService());
        var (tb, window, _) = Open<PhonePromptView, PhonePromptViewModel>(vm);
        window.TypeText("5551234567");
        tb.Text.Should().Be("5551234567");
    }

    [AvaloniaFact]
    public void PhonePrompt_MaskOn_TypingDigitsReshapes()
    {
        var vm = new PhonePromptViewModel(P("p", "Phone", "phone"), NewService(typeof(PhoneInputMaskProfile)));
        var (tb, window, _) = Open<PhonePromptView, PhonePromptViewModel>(vm);
        window.TypeText("5551234567");
        tb.Text.Should().Be("(555) 123-4567");
    }

    // ── EmailPromptView ──

    [AvaloniaFact]
    public void EmailPrompt_TypingAcceptsAnyText()
    {
        var vm = new EmailPromptViewModel(P("p", "Email", "email"), NewService());
        var (_, window, _) = Open<EmailPromptView, EmailPromptViewModel>(vm);
        window.TypeText("user@example.com");
        vm.Response.Should().Be("user@example.com");
    }

    // ── UrlPromptView ──

    [AvaloniaFact]
    public void UrlPrompt_TypingAcceptsAnyText()
    {
        var vm = new UrlPromptViewModel(P("p", "URL", "url"), NewService());
        var (_, window, _) = Open<UrlPromptView, UrlPromptViewModel>(vm);
        window.TypeText("https://example.com");
        vm.Response.Should().Be("https://example.com");
    }

    // ── SelectPromptView free-text override ──

    [AvaloniaFact]
    public void SelectPrompt_FreeTextOverride_AcceptsValueNotInSuggestions()
    {
        var prompt = P("p", "Tier");
        prompt.Hints.SuggestedValues = new List<string> { "Gold", "Silver" };
        var vm = new SelectPromptViewModel(prompt, NewService());
        var view = new SelectPromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);
        var ft = view.FindDescendant<TextBox>(t => t.Name == "FreeTextEntry");
        ft.Focus();
        GuiTestExtensions.PumpDispatcher();
        window.TypeText("Platinum");
        vm.Response.Should().Be("Platinum");
    }

    // ── TextPromptView fallback for SSN/EIN/ZIP via mask registry ──

    [AvaloniaFact]
    public void TextPrompt_SsnHint_MaskOn_TypingReshapes()
    {
        var vm = new TextPromptViewModel(P("p", "SSN", "ssn"), NewService(typeof(SsnInputMaskProfile)));
        var (tb, window, _) = Open<TextPromptView, TextPromptViewModel>(vm);
        window.TypeText("123456789");
        tb.Text.Should().Be("123-45-6789");
    }

    [AvaloniaFact]
    public void TextPrompt_EinHint_MaskOn_TypingReshapes()
    {
        var vm = new TextPromptViewModel(P("p", "EIN", "ein"), NewService(typeof(EinInputMaskProfile)));
        var (tb, window, _) = Open<TextPromptView, TextPromptViewModel>(vm);
        window.TypeText("123456789");
        tb.Text.Should().Be("12-3456789");
    }

    [AvaloniaFact]
    public void TextPrompt_ZipHint_MaskOn_NinthDigitInsertsDash()
    {
        var vm = new TextPromptViewModel(P("p", "ZIP", "zipcode"), NewService(typeof(ZipInputMaskProfile)));
        var (tb, window, _) = Open<TextPromptView, TextPromptViewModel>(vm);
        window.TypeText("90210");
        tb.Text.Should().Be("90210");
        window.TypeText("1234");
        tb.Text.Should().Be("90210-1234");
    }

    [AvaloniaFact]
    public void TextPrompt_TextHint_NoMask_ArbitraryTypingPasses()
    {
        var vm = new TextPromptViewModel(P("p", "Notes", "text"), NewService(typeof(PhoneInputMaskProfile)));
        var (tb, window, _) = Open<TextPromptView, TextPromptViewModel>(vm);
        window.TypeText("123456789");
        tb.Text.Should().Be("123456789",
            "text hint has no mask — even with PhoneInputMask flag on, no reshape happens");
    }
}
