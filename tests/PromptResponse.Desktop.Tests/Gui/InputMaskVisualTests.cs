using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views.Prompts;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// Visual progression tests for input-mask auto-formatting. Where
/// <see cref="InputMaskGuiTests"/> verifies the final TextBox text after a bulk
/// type, these tests type one character at a time and assert the *visible*
/// TextBox text after every keystroke — catching mid-typing reshape bugs:
///   * Boundary insertions in the wrong place ("(55)" instead of "(55")
///   * Caret jumping past the just-typed character
///   * Free-text intermixed with digits getting wrongly reshaped
///   * Display-only previews showing stale formatted values
/// All keystrokes go through <see cref="HeadlessWindowExtensions.KeyTextInput"/>,
/// the same path real keyboard input takes through the Avalonia headless harness.
/// </summary>
public class InputMaskVisualTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static IProfileService NewService(params Type[] enabledFlags)
    {
        var svc = new ProfileService(new FixedProbe());
        foreach (var t in enabledFlags)
        {
            // generic Enable<T>() via reflection — keeps test data tables readable.
            var enableMethod = typeof(IProfileService).GetMethod(nameof(IProfileService.Enable))!.MakeGenericMethod(t);
            enableMethod.Invoke(svc, null);
        }
        return svc;
    }

    private static Prompt P(string id, string label, string type, string response = "") =>
        new() { Id = id, Label = label, Response = response, Hints = new PromptHints { ExpectedDataType = type } };

    private static (TextBox tb, Avalonia.Controls.Window window) OpenPhone(IProfileService service)
    {
        var vm = new PhonePromptViewModel(P("p", "Phone", "phone"), service);
        var view = new PhonePromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);
        var tb = view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        tb.Focus();
        GuiTestExtensions.PumpDispatcher();
        return (tb, window);
    }

    [AvaloniaFact]
    public void Phone_DigitByDigit_VisibleTextProgressesThroughBoundaryInsertions()
    {
        var (tb, window) = OpenPhone(NewService(typeof(PhoneInputMaskProfile)));

        // Each row asserts visible text after pressing the next digit.
        var progression = new[]
        {
            ("5", "(5"),
            ("5", "(55"),
            ("5", "(555"),
            ("1", "(555) 1"),
            ("2", "(555) 12"),
            ("3", "(555) 123"),
            ("4", "(555) 123-4"),
            ("5", "(555) 123-45"),
            ("6", "(555) 123-456"),
            ("7", "(555) 123-4567"),
        };
        foreach (var (key, expected) in progression)
        {
            window.TypeText(key);
            tb.Text.Should().Be(expected,
                $"after typing '{key}' the visible TextBox should show the in-progress reshape '{expected}'");
        }
    }

    [AvaloniaFact]
    public void Phone_FreeTextMidTyping_DoesNotReshape()
    {
        var (tb, window) = OpenPhone(NewService(typeof(PhoneInputMaskProfile)));

        window.TypeText("see HR");

        // Vision invariant — non-numeric responses pass through untouched.
        tb.Text.Should().Be("see HR");
    }

    [AvaloniaFact]
    public void Phone_FlagOff_RawDigitsStayRaw()
    {
        // Universal-core: with no mask flag, the user gets exactly what they typed.
        var (tb, window) = OpenPhone(NewService());  // no flags

        window.TypeText("5551234567");

        tb.Text.Should().Be("5551234567",
            "without PhoneInputMaskProfile, no flag should auto-reshape the user's input");
    }

    [AvaloniaFact]
    public void Ssn_DigitByDigit_VisibleTextProgresses()
    {
        var service = NewService(typeof(SsnInputMaskProfile));
        var vm = new TextPromptViewModel(P("s", "SSN", "ssn"), service);
        var view = new TextPromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);
        var tb = view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        tb.Focus();
        GuiTestExtensions.PumpDispatcher();

        var progression = new[]
        {
            ("1", "1"),
            ("2", "12"),
            ("3", "123"),
            ("4", "123-4"),
            ("5", "123-45"),
            ("6", "123-45-6"),
            ("7", "123-45-67"),
            ("8", "123-45-678"),
            ("9", "123-45-6789"),
        };
        foreach (var (key, expected) in progression)
        {
            window.TypeText(key);
            tb.Text.Should().Be(expected, $"after '{key}' SSN should be '{expected}'");
        }
    }

    [AvaloniaFact]
    public void Ein_DigitByDigit_VisibleTextProgresses()
    {
        var service = NewService(typeof(EinInputMaskProfile));
        var vm = new TextPromptViewModel(P("e", "EIN", "ein"), service);
        var view = new TextPromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);
        var tb = view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        tb.Focus();
        GuiTestExtensions.PumpDispatcher();

        var progression = new[] { ("1","1"), ("2","12"), ("3","12-3"), ("4","12-34"), ("5","12-345") };
        foreach (var (key, expected) in progression)
        {
            window.TypeText(key);
            tb.Text.Should().Be(expected);
        }
    }

    [AvaloniaFact]
    public void ZipPlus4_NinthDigit_TriggersDashInsertion()
    {
        var service = NewService(typeof(ZipInputMaskProfile));
        var vm = new TextPromptViewModel(P("z", "ZIP", "zipcode"), service);
        var view = new TextPromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);
        var tb = view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        tb.Focus();
        GuiTestExtensions.PumpDispatcher();

        // 5 digits stays raw (still a valid US ZIP)
        window.TypeText("90210");
        tb.Text.Should().Be("90210");
        // 6th digit triggers ZIP+4 reshape — dash inserted between 5th and 6th
        window.TypeText("1");
        tb.Text.Should().Be("90210-1");
        window.TypeText("234");
        tb.Text.Should().Be("90210-1234");
    }

    [AvaloniaFact]
    public void CaretPosition_AfterReshape_LandsAtEndOfTypedDigits()
    {
        // When the mask inserts delimiters, the caret must end up just after the
        // most-recently-typed digit so the next keystroke continues the natural flow.
        var (tb, window) = OpenPhone(NewService(typeof(PhoneInputMaskProfile)));

        window.TypeText("555");
        tb.Text.Should().Be("(555");
        tb.CaretIndex.Should().Be(4, "caret should sit just after the third digit");

        window.TypeText("1");
        tb.Text.Should().Be("(555) 1");
        tb.CaretIndex.Should().Be(7, "caret should sit just after the fourth digit");

        window.TypeText("23");
        tb.Text.Should().Be("(555) 123");
        tb.CaretIndex.Should().Be(9);

        window.TypeText("4567");
        tb.Text.Should().Be("(555) 123-4567");
        tb.CaretIndex.Should().Be(14);
    }

    [AvaloniaFact]
    public void NumberPrompt_DisplaysAsPreview_VisibleAfterFlagsActive()
    {
        // The "Displays as: 42,000" preview is gated on DisplaysAsPreview AND the
        // specific display flag (NumberThousandsSeparators) being on. Without both,
        // the preview is hidden — universal core.
        var service = NewService(typeof(NumberThousandsSeparatorsProfile), typeof(DisplaysAsPreviewProfile));
        var vm = new NumberPromptViewModel(P("n", "Salary", "number"), service);
        var view = new NumberPromptView { DataContext = vm };
        var window = view.ShowInWindow(width: 600, height: 200);
        var tb = view.FindDescendant<TextBox>(t => t.Name == "ResponseTextBox");
        var hint = view.FindDescendant<TextBlock>(t => t.Name == "DisplayHint");
        tb.Focus();
        GuiTestExtensions.PumpDispatcher();

        hint.IsVisible.Should().BeFalse("preview hidden when there's no value yet");

        window.TypeText("42000");
        GuiTestExtensions.PumpDispatcher();

        hint.IsVisible.Should().BeTrue("preview should appear once the formatter has something to show");
        hint.Text.Should().Contain("42,000",
            "the preview must show the formatted version, not the raw stored value");
        tb.Text.Should().Be("42000",
            "the underlying input value stays raw — display affordance never mutates the response");
    }
}
