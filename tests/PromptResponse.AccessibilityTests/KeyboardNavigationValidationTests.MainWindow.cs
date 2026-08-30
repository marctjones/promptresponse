using AwesomeAssertions;
using System.Text.RegularExpressions;

namespace PromptResponse.AccessibilityTests;

/// <summary>Validates keyboard access to the main window.</summary>
public class KeyboardNavigationMainWindowTests
{
    [Fact]
    public void MainWindow_CommonActions_ShouldHave_KeyboardShortcuts()
    {
        var xamlContent = KeyboardNavigationTestFiles.ReadDesktopFile("Views/MainShellView.axaml");
        if (xamlContent is null) return;

        var shortcuts = new Dictionary<string, string>
        {
            { "Ctrl+N", "New template" }, { "Ctrl+O", "Open" },
            { "Ctrl+S", "Save" }, { "Ctrl+Shift+S", "Save As" }, { "Ctrl+W", "Close" },
        };

        foreach (var shortcut in shortcuts)
            xamlContent.Should().Contain(shortcut.Key, $"because {shortcut.Value} should have keyboard shortcut {shortcut.Key} for keyboard-only users");
    }

    [Fact]
    public void MainWindow_Menus_ShouldHave_MnemonicsForTopLevel()
    {
        var xamlContent = KeyboardNavigationTestFiles.ReadDesktopFile("Views/MainShellView.axaml");
        if (xamlContent is null) return;

        foreach (var mnemonic in new[] { "_File", "_View", "_Help" })
            xamlContent.Should().Contain($"Header=\"{mnemonic}\"", $"because top-level menu {mnemonic} should have mnemonic for Alt+{mnemonic[1]} access");
    }

    [Fact]
    public void ViewModels_Commands_ShouldBe_KeyboardAccessible()
    {
        var viewModelContent = KeyboardNavigationTestFiles.ReadDesktopFile("ViewModels/MainShellViewModel.cs");
        if (viewModelContent is null) return;

        var explicitPattern = @"public\s+(?:I(?:Input)?Command\s+\w+Command|IRelayCommand\w*\s+\w+Command)";
        var generatedPattern = @"\[RelayCommand[^\]]*\]";
        var commandCount = Regex.Matches(viewModelContent, explicitPattern).Count + Regex.Matches(viewModelContent, generatedPattern).Count;

        commandCount.Should().BeGreaterThan(0, "because commands in view models should be exposed for keyboard shortcuts and menu access");
        viewModelContent.Should().Contain("Open", "because Open command should be accessible via Ctrl+O");
        viewModelContent.Should().Contain("Save", "because Save command should be accessible via Ctrl+S");
    }
}
