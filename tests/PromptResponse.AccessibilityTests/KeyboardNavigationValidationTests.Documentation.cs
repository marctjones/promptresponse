using AwesomeAssertions;

namespace PromptResponse.AccessibilityTests;

/// <summary>Validates documented keyboard workflows for users and testers.</summary>
public class KeyboardNavigationDocumentationTests
{
    [Fact]
    public void KeyboardShortcuts_ShouldBe_Documented()
    {
        var content = KeyboardNavigationTestFiles.ReadDocumentation();
        if (content is null) return;

        foreach (var shortcut in new[] { "Ctrl+O", "Ctrl+S", "Ctrl+Shift+S", "Alt+F", "Alt+V", "Alt+H" })
            content.Should().Contain(shortcut, $"because keyboard shortcut {shortcut} should be documented in UX_ACCESSIBILITY.md so users know how to navigate without a mouse");
    }

    [Fact]
    public void AccessibilityGuide_ShouldDocument_KeyboardTesting()
    {
        var content = KeyboardNavigationTestFiles.ReadDocumentation();
        if (content is null) return;

        content.Should().Contain("Keyboard Navigation", "because UX_ACCESSIBILITY.md should document keyboard navigation testing");
        content.Should().Contain("Tab", "because Tab key usage should be documented for testers");
        content.Should().Contain("focus", "because focus visibility and focus order should be discussed");
        (content.Contains("keyboard") && (content.Contains("checklist") || content.Contains("- [ ]"))).Should().BeTrue("because UX_ACCESSIBILITY.md should include a keyboard testing checklist");
    }
}
