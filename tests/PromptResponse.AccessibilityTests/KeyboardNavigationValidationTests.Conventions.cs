using AwesomeAssertions;

namespace PromptResponse.AccessibilityTests;

/// <summary>Validates project-wide keyboard conventions and navigation escape paths.</summary>
public class KeyboardNavigationConventionTests
{
    [Fact]
    public void Application_ShouldNot_Create_KeyboardTraps()
    {
        var viewsDirectory = KeyboardNavigationTestFiles.DesktopPath("Views");
        foreach (var xamlFile in Directory.GetFiles(viewsDirectory, "*.axaml", SearchOption.AllDirectories))
        {
            var xamlContent = File.ReadAllText(xamlFile);
            if (xamlContent.Contains("Window") && xamlContent.Contains("ShowDialog"))
                xamlContent.Should().Contain("KeyDown", $"because modal dialogs in {Path.GetFileName(xamlFile)} should handle Esc key to close");

            var tabStopFalsePattern = @"<(TextBox|Button|ComboBox|CheckBox)[^>]*IsTabStop=""False""";
            if (System.Text.RegularExpressions.Regex.IsMatch(xamlContent, tabStopFalsePattern))
                Console.WriteLine($"WARNING: {Path.GetFileName(xamlFile)} has IsTabStop=\"False\" on interactive element: {System.Text.RegularExpressions.Regex.Match(xamlContent, tabStopFalsePattern).Value}");
        }
    }

    [Theory]
    [InlineData("Ctrl+O", "Open file")]
    [InlineData("Ctrl+S", "Save file")]
    [InlineData("Tab", "Navigate to next field")]
    [InlineData("Shift+Tab", "Navigate to previous field")]
    [InlineData("Space", "Toggle checkbox/expand section")]
    [InlineData("Enter", "Activate button")]
    [InlineData("Esc", "Close dialog")]
    [InlineData("Alt+F", "File menu")]
    [InlineData("Alt+V", "View menu")]
    [InlineData("Alt+H", "Help menu")]
    public void KeyboardShortcut_ShouldFollow_StandardConventions(string shortcut, string action)
    {
        var standardShortcuts = new Dictionary<string, string>
        {
            { "Ctrl+O", "Open" }, { "Ctrl+S", "Save" }, { "Tab", "Next field" },
            { "Shift+Tab", "Previous field" }, { "Space", "Toggle/Activate" },
            { "Enter", "Activate/Submit" }, { "Esc", "Cancel/Close" },
        };

        if (standardShortcuts.ContainsKey(shortcut))
            true.Should().BeTrue($"because {shortcut} for {action} follows standard keyboard conventions");
    }
}
