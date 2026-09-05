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
}
