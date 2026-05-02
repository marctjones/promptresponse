using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AwesomeAssertions;
using Xunit;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// Regression tests for the GUI test harness itself. These pin the supported
/// real-input paths so the harness doesn't silently regress to fake input.
///
/// Two known Avalonia.Headless limitations are NOT regressions but documented
/// constraints (see <see cref="GuiTestExtensions.Activate"/> xmldoc):
///   * <c>MouseDown/MouseUp</c> synthesis does not fire Click on Buttons
///   * <c>Space</c> on a focused CheckBox/RadioButton does not toggle IsChecked
/// The harness routes around both.
/// </summary>
public class GuiHarnessRegressionTests
{
    [AvaloniaFact]
    public void Activate_FiresButtonClick_ViaRealKeyboardEnter()
    {
        var fired = 0;
        var btn = new Button { Content = "Click me", MinWidth = 200, MinHeight = 60 };
        btn.Click += (_, _) => fired++;
        var window = btn.ShowInWindow(width: 400, height: 200);

        window.Activate(btn);

        fired.Should().Be(1,
            "Button activation must go through the real keyboard input pipeline (focus + Enter)");
    }

    [AvaloniaFact]
    public void Activate_TogglesCheckBox_IsCheckedFlipsAndPropertyChangedFires()
    {
        var changes = 0;
        var cb = new CheckBox { Content = "Toggle", MinWidth = 200, MinHeight = 40 };
        cb.IsCheckedChanged += (_, _) => changes++;
        var window = cb.ShowInWindow(width: 400, height: 200);

        cb.IsChecked.Should().BeFalse();
        window.Activate(cb);
        cb.IsChecked.Should().BeTrue();
        changes.Should().BeGreaterThan(0, "IsCheckedChanged must fire so bound consumers update");

        window.Activate(cb);
        cb.IsChecked.Should().BeFalse("Activate toggles the current state");
    }

    [AvaloniaFact]
    public void Activate_SelectsRadioButton_AndIsCheckedChangedFires()
    {
        var changes = 0;
        var rb1 = new RadioButton { Content = "A", GroupName = "g", MinHeight = 40 };
        var rb2 = new RadioButton { Content = "B", GroupName = "g", MinHeight = 40 };
        rb1.IsCheckedChanged += (_, _) => changes++;
        var stack = new StackPanel { Children = { rb1, rb2 } };
        var window = stack.ShowInWindow(width: 400, height: 200);

        window.Activate(rb1);

        rb1.IsChecked.Should().BeTrue();
        changes.Should().BeGreaterThan(0);
    }
}
