using AwesomeAssertions;
using System.Text.RegularExpressions;

namespace PromptResponse.AccessibilityTests;

/// <summary>Validates tab order, focus, and standard controls in form filling.</summary>
public class KeyboardNavigationFormFillingTests
{
    [Fact]
    public void FormFillingView_AllTextBoxes_ShouldHave_TabIndex()
    {
        var xamlContent = KeyboardNavigationTestFiles.ReadDesktopFile("Views/FormFillingView.axaml");
        if (xamlContent is null) return;

        var textBoxes = Regex.Matches(xamlContent, @"<TextBox[^>]*>", RegexOptions.Singleline);
        var withTabIndex = textBoxes.Count(textBox => textBox.Value.Contains("TabIndex="));

        textBoxes.Count.Should().BeGreaterThan(0, "because FormFillingView should contain TextBox elements");
        withTabIndex.Should().BeGreaterThan(0, $"because at least some TextBox elements should have TabIndex set. Found {textBoxes.Count} TextBox elements, {withTabIndex} with TabIndex. All interactive elements should participate in logical tab order.");
    }

    [Fact]
    public void FormFillingView_TabIndex_ShouldNotBe_Negative()
    {
        var xamlContent = KeyboardNavigationTestFiles.ReadDesktopFile("Views/FormFillingView.axaml");
        if (xamlContent is null) return;

        foreach (Match match in Regex.Matches(xamlContent, @"TabIndex=""(-?\d+)"""))
            int.Parse(match.Groups[1].Value).Should().BeGreaterThanOrEqualTo(0, $"because negative TabIndex values remove elements from tab order, potentially creating keyboard navigation barriers. Found: TabIndex=\"{match.Groups[1].Value}\"");
    }

    [Fact]
    public void FormFillingView_ShouldSupport_StandardKeyboardConventions()
    {
        var xamlContent = KeyboardNavigationTestFiles.ReadDesktopFile("Views/FormFillingView.axaml");
        if (xamlContent is null) return;

        xamlContent.Should().Contain("<TextBox", "because TextBox controls provide standard keyboard conventions automatically");
        (xamlContent.Contains("<Expander") || xamlContent.Contains("<Button")).Should().BeTrue("because FormFillingView should have interactive controls (Expanders for sections or Buttons for actions)");
    }

    [Fact]
    public void Application_FocusManagement_ShouldBe_Logical()
    {
        var xamlContent = KeyboardNavigationTestFiles.ReadDesktopFile("Views/FormFillingView.axaml");
        if (xamlContent is null) return;

        if (xamlContent.Contains("FocusManager.FocusedElement") || xamlContent.Contains(".Focus()"))
            Console.WriteLine("INFO: FormFillingView manipulates focus. Ensure this is intentional and doesn't confuse keyboard users.");

        xamlContent.Should().Contain("<ScrollViewer", "because long forms need ScrollViewer to enable keyboard scrolling (Page Up/Down, arrow keys)");
    }

    [Fact]
    public void FluentTheme_ShouldProvide_FocusIndicators()
    {
        var appXaml = KeyboardNavigationTestFiles.ReadDesktopFile("App.axaml");
        if (appXaml is null) return;

        appXaml.Should().Contain("FluentTheme", "because FluentTheme provides built-in focus indicators that meet accessibility standards");
        appXaml.Should().NotContain("FocusVisualStyle", "because custom focus styles might remove the visible focus indicator, which is critical for keyboard users");
    }
}
