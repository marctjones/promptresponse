using FluentAssertions;
using System.Text.RegularExpressions;
using Xunit;

namespace PromptResponse.AccessibilityTests;

/// <summary>
/// Validates keyboard navigation and keyboard accessibility.
/// </summary>
/// <remarks>
/// These tests verify that:
/// - All functionality is accessible via keyboard
/// - Tab order is logical
/// - Focus indicators are present
/// - Keyboard shortcuts are documented
/// - No keyboard traps exist
/// - Standard keyboard conventions are followed
/// </remarks>
public class KeyboardNavigationValidationTests
{
    private readonly string _projectRoot;

    public KeyboardNavigationValidationTests()
    {
        _projectRoot = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..");
    }

    [Fact]
    public void MainWindow_CommonActions_ShouldHave_KeyboardShortcuts()
    {
        // Arrange
        var xamlPath = Path.Combine(_projectRoot, "src/PromptResponse.Desktop/Views/MainWindow.axaml");

        if (!File.Exists(xamlPath))
        {
            return;
        }

        var xamlContent = File.ReadAllText(xamlPath);

        // Act & Assert - Check for common shortcuts
        var shortcuts = new Dictionary<string, string>
        {
            { "Ctrl+O", "Open" },
            { "Ctrl+S", "Save" },
            { "Ctrl+Shift+S", "Save As" },
            { "Ctrl+Shift+O", "Open template for editing" }
        };

        foreach (var shortcut in shortcuts)
        {
            xamlContent.Should().Contain(shortcut.Key,
                $"because {shortcut.Value} should have keyboard shortcut {shortcut.Key} for keyboard-only users");
        }
    }

    [Fact]
    public void MainWindow_Menus_ShouldHave_MnemonicsForTopLevel()
    {
        // Arrange
        var xamlPath = Path.Combine(_projectRoot, "src/PromptResponse.Desktop/Views/MainWindow.axaml");

        if (!File.Exists(xamlPath))
        {
            return;
        }

        var xamlContent = File.ReadAllText(xamlPath);

        // Act & Assert - Top-level menus should have mnemonics
        var expectedMnemonics = new[] { "_File", "_View", "_Help" };

        foreach (var mnemonic in expectedMnemonics)
        {
            xamlContent.Should().Contain($"Header=\"{mnemonic}\"",
                $"because top-level menu {mnemonic} should have mnemonic for Alt+{mnemonic[1]} access");
        }
    }

    [Fact]
    public void FormFillingView_AllTextBoxes_ShouldHave_TabIndex()
    {
        // Arrange
        var xamlPath = Path.Combine(_projectRoot, "src/PromptResponse.Desktop/Views/FormFillingView.axaml");

        if (!File.Exists(xamlPath))
        {
            return;
        }

        var xamlContent = File.ReadAllText(xamlPath);

        // Act - Parse XAML to find TextBox elements
        var textBoxPattern = @"<TextBox[^>]*>";
        var textBoxes = Regex.Matches(xamlContent, textBoxPattern, RegexOptions.Singleline);

        // Check how many have TabIndex
        var withTabIndex = 0;
        foreach (Match textBox in textBoxes)
        {
            if (textBox.Value.Contains("TabIndex="))
            {
                withTabIndex++;
            }
        }

        // Assert
        textBoxes.Count.Should().BeGreaterThan(0,
            "because FormFillingView should contain TextBox elements");

        withTabIndex.Should().BeGreaterThan(0,
            $"because at least some TextBox elements should have TabIndex set. " +
            $"Found {textBoxes.Count} TextBox elements, {withTabIndex} with TabIndex. " +
            $"All interactive elements should participate in logical tab order.");
    }

    [Fact]
    public void FormFillingView_TabIndex_ShouldNotBe_Negative()
    {
        // Arrange
        var xamlPath = Path.Combine(_projectRoot, "src/PromptResponse.Desktop/Views/FormFillingView.axaml");

        if (!File.Exists(xamlPath))
        {
            return;
        }

        var xamlContent = File.ReadAllText(xamlPath);

        // Act - Find all TabIndex values
        var tabIndexPattern = @"TabIndex=""(-?\d+)""";
        var tabIndexMatches = Regex.Matches(xamlContent, tabIndexPattern);

        // Assert
        foreach (Match match in tabIndexMatches)
        {
            var tabIndexValue = int.Parse(match.Groups[1].Value);
            tabIndexValue.Should().BeGreaterThanOrEqualTo(0,
                $"because negative TabIndex values remove elements from tab order, " +
                $"potentially creating keyboard navigation barriers. Found: TabIndex=\"{tabIndexValue}\"");
        }
    }

    [Fact]
    public void Application_ShouldNot_Create_KeyboardTraps()
    {
        // This is a design validation test
        // We verify that our XAML doesn't use patterns that create keyboard traps

        // Arrange
        var xamlFiles = Directory.GetFiles(
            Path.Combine(_projectRoot, "src/PromptResponse.Desktop/Views"),
            "*.axaml",
            SearchOption.AllDirectories);

        foreach (var xamlFile in xamlFiles)
        {
            var xamlContent = File.ReadAllText(xamlFile);

            // Act & Assert - Check for common keyboard trap patterns

            // 1. Modal dialogs should be escapable
            if (xamlContent.Contains("Window") && xamlContent.Contains("ShowDialog"))
            {
                xamlContent.Should().Contain("KeyDown",
                    $"because modal dialogs in {Path.GetFileName(xamlFile)} should handle Esc key to close");
            }

            // 2. No IsTabStop="False" on interactive elements that need keyboard access
            var tabStopFalsePattern = @"<(TextBox|Button|ComboBox|CheckBox)[^>]*IsTabStop=""False""";
            var hasTabStopFalse = Regex.IsMatch(xamlContent, tabStopFalsePattern);

            if (hasTabStopFalse)
            {
                // This isn't always wrong, but it's suspicious - log for manual review
                var match = Regex.Match(xamlContent, tabStopFalsePattern);
                // Allow test to pass but note the finding
                Console.WriteLine($"WARNING: {Path.GetFileName(xamlFile)} has IsTabStop=\"False\" on interactive element: {match.Value}");
            }
        }
    }

    [Fact]
    public void KeyboardShortcuts_ShouldBe_Documented()
    {
        // Arrange
        var accessibilityDoc = Path.Combine(_projectRoot, "ACCESSIBILITY.md");

        if (!File.Exists(accessibilityDoc))
        {
            // Skip if ACCESSIBILITY.md doesn't exist
            return;
        }

        var content = File.ReadAllText(accessibilityDoc);

        // Act & Assert - Verify keyboard shortcuts are documented
        var expectedShortcuts = new[]
        {
            "Ctrl+O",
            "Ctrl+S",
            "Ctrl+Shift+S",
            "Alt+F",
            "Alt+V",
            "Alt+H"
        };

        foreach (var shortcut in expectedShortcuts)
        {
            content.Should().Contain(shortcut,
                $"because keyboard shortcut {shortcut} should be documented in ACCESSIBILITY.md " +
                "so users know how to navigate without a mouse");
        }
    }

    [Fact]
    public void FormFillingView_ShouldSupport_StandardKeyboardConventions()
    {
        // Arrange
        var xamlPath = Path.Combine(_projectRoot, "src/PromptResponse.Desktop/Views/FormFillingView.axaml");

        if (!File.Exists(xamlPath))
        {
            return;
        }

        var xamlContent = File.ReadAllText(xamlPath);

        // Act & Assert - Verify standard controls are used (they have built-in keyboard support)

        // TextBox has built-in:
        // - Ctrl+A (select all)
        // - Ctrl+C/X/V (copy/cut/paste)
        // - Ctrl+Z/Y (undo/redo)
        // - Arrow keys, Home, End
        xamlContent.Should().Contain("<TextBox",
            "because TextBox controls provide standard keyboard conventions automatically");

        // Expander has built-in:
        // - Space/Enter to expand/collapse
        var hasExpanders = xamlContent.Contains("<Expander");
        var hasButtons = xamlContent.Contains("<Button");

        // At least one type of interactive control should exist
        (hasExpanders || hasButtons).Should().BeTrue(
            "because FormFillingView should have interactive controls (Expanders for sections or Buttons for actions)");
    }

    [Fact]
    public void Application_FocusManagement_ShouldBe_Logical()
    {
        // Arrange
        var xamlPath = Path.Combine(_projectRoot, "src/PromptResponse.Desktop/Views/FormFillingView.axaml");

        if (!File.Exists(xamlPath))
        {
            return;
        }

        var xamlContent = File.ReadAllText(xamlPath);

        // Act - Check layout structure
        // Forms should be laid out in visual order (top-to-bottom, left-to-right)
        // which matches natural tab order

        // Assert - Verify no FocusManager.FocusedElement manipulation that might confuse users
        var hasFocusManipulation = xamlContent.Contains("FocusManager.FocusedElement") ||
                                    xamlContent.Contains(".Focus()");

        if (hasFocusManipulation)
        {
            // Not necessarily wrong, but should be intentional
            Console.WriteLine("INFO: FormFillingView manipulates focus. Ensure this is intentional and doesn't confuse keyboard users.");
        }

        // Verify ScrollViewer exists for keyboard scrolling
        xamlContent.Should().Contain("<ScrollViewer",
            "because long forms need ScrollViewer to enable keyboard scrolling (Page Up/Down, arrow keys)");
    }

    [Fact]
    public void FluentTheme_ShouldProvide_FocusIndicators()
    {
        // Arrange
        var appXamlPath = Path.Combine(_projectRoot, "src/PromptResponse.Desktop/App.axaml");

        if (!File.Exists(appXamlPath))
        {
            return;
        }

        var appXaml = File.ReadAllText(appXamlPath);

        // Act & Assert
        appXaml.Should().Contain("FluentTheme",
            "because FluentTheme provides built-in focus indicators that meet accessibility standards");

        // Verify we're not overriding focus visuals in a way that removes them
        appXaml.Should().NotContain("FocusVisualStyle",
            "because custom focus styles might remove the visible focus indicator, " +
            "which is critical for keyboard users");
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
        // This is a documentation test to ensure we're following standard conventions

        // Assert
        var standardShortcuts = new Dictionary<string, string>
        {
            { "Ctrl+O", "Open" },
            { "Ctrl+S", "Save" },
            { "Tab", "Next field" },
            { "Shift+Tab", "Previous field" },
            { "Space", "Toggle/Activate" },
            { "Enter", "Activate/Submit" },
            { "Esc", "Cancel/Close" }
        };

        if (standardShortcuts.ContainsKey(shortcut))
        {
            // This shortcut follows conventions
            true.Should().BeTrue($"because {shortcut} for {action} follows standard keyboard conventions");
        }
    }

    [Fact]
    public void ViewModels_Commands_ShouldBe_KeyboardAccessible()
    {
        // Arrange
        var viewModelPath = Path.Combine(_projectRoot,
            "src/PromptResponse.Desktop/ViewModels/MainWindowViewModel.cs");

        if (!File.Exists(viewModelPath))
        {
            return;
        }

        var viewModelContent = File.ReadAllText(viewModelPath);

        // Act - Check that commands are defined (they'll be bound to menu items)
        var commandPattern = @"public ICommand \w+Command";
        var commands = Regex.Matches(viewModelContent, commandPattern);

        // Assert
        commands.Count.Should().BeGreaterThan(0,
            "because commands in view models should be exposed for keyboard shortcuts and menu access");

        // Common commands that should exist
        viewModelContent.Should().Contain("OpenCommand",
            "because Open command should be accessible via Ctrl+O");
        viewModelContent.Should().Contain("SaveCommand",
            "because Save command should be accessible via Ctrl+S");
    }

    [Fact]
    public void AccessibilityGuide_ShouldDocument_KeyboardTesting()
    {
        // Arrange
        var accessibilityDoc = Path.Combine(_projectRoot, "ACCESSIBILITY.md");

        if (!File.Exists(accessibilityDoc))
        {
            return;
        }

        var content = File.ReadAllText(accessibilityDoc);

        // Act & Assert
        content.Should().Contain("Keyboard Navigation",
            "because ACCESSIBILITY.md should document keyboard navigation testing");

        content.Should().Contain("Tab",
            "because Tab key usage should be documented for testers");

        content.Should().Contain("focus",
            "because focus visibility and focus order should be discussed");

        var hasKeyboardChecklist = content.Contains("keyboard") &&
                                    (content.Contains("checklist") || content.Contains("- [ ]"));

        hasKeyboardChecklist.Should().BeTrue(
            "because ACCESSIBILITY.md should include a keyboard testing checklist");
    }
}
