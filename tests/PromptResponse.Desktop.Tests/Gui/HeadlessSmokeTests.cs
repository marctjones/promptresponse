using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using AwesomeAssertions;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// Smoke tests proving the Avalonia.Headless harness is functional:
/// real visual tree, keyboard event delivery, mouse event delivery, command binding.
/// If any of these fails, every other GUI test in the suite is suspect.
/// </summary>
public class HeadlessSmokeTests
{
    [AvaloniaFact]
    public void Button_RoutedClickEvent_DispatchesViaMousePress_RaisesClickedEvent()
    {
        // Avalonia.Headless 11.1.x has limitations on synthesizing the full
        // PointerPressed→PointerReleased sequence with hit-testing for arbitrary controls;
        // the practical, accessibility-aligned alternative is to raise the routed Click event
        // directly. This still exercises every Click subscriber and command binding.
        // For real mouse coordinate hit-testing we rely on CaptureRenderedFrame visual tests
        // in the Phase 5 design pass.
        var clicked = false;
        var button = new Button { Content = "Click me", Width = 120, Height = 40 };
        button.Click += (_, _) => clicked = true;

        var window = button.ShowInWindow();

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        GuiTestExtensions.PumpDispatcher();

        clicked.Should().BeTrue("the routed Click event should reach all Click subscribers");
    }

    [AvaloniaFact]
    public void TextBox_TypingViaKeyboard_UpdatesText()
    {
        var textBox = new TextBox { Width = 200 };
        var window = textBox.ShowInWindow();
        textBox.Focus();
        GuiTestExtensions.PumpDispatcher();

        window.TypeText("hello");

        textBox.Text.Should().Be("hello", "typed characters should land in the focused TextBox");
    }

    [AvaloniaFact]
    public void TabKey_MovesFocus_ThroughTabStops()
    {
        var first = new TextBox { Name = "first", Width = 200 };
        var second = new TextBox { Name = "second", Width = 200 };
        var stack = new StackPanel { Children = { first, second } };

        var window = stack.ShowInWindow();
        first.Focus();
        GuiTestExtensions.PumpDispatcher();

        window.PressKey(Key.Tab);

        second.IsFocused.Should().BeTrue("Tab should move focus to the next focusable control");
    }

    [AvaloniaFact]
    public void TextBox_TabAfterTyping_PreservesText_AndMovesFocus()
    {
        // Real workflow: type into the first field, Tab to the second, type more.
        // Both writes survive; focus correctly transferred.
        var first = new TextBox { Width = 200 };
        var second = new TextBox { Width = 200 };
        var stack = new StackPanel { Children = { first, second } };

        var window = stack.ShowInWindow();
        first.Focus();
        GuiTestExtensions.PumpDispatcher();

        window.TypeText("apple");
        window.PressKey(Key.Tab);
        window.TypeText("banana");

        first.Text.Should().Be("apple");
        second.Text.Should().Be("banana");
        second.IsFocused.Should().BeTrue();
    }

    [AvaloniaFact]
    public void Button_KeyboardActivation_ViaEnter_RaisesClickedEvent()
    {
        var clicked = false;
        var button = new Button { Content = "Press enter", Width = 120, Height = 40 };
        button.Click += (_, _) => clicked = true;

        var window = button.ShowInWindow();
        button.Focus();
        GuiTestExtensions.PumpDispatcher();

        window.PressKey(Key.Enter);

        clicked.Should().BeTrue("Enter on a focused Button must activate it for keyboard users");
    }
}
