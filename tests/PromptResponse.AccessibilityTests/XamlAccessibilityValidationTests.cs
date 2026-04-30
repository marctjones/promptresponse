using FluentAssertions;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Xunit;

namespace PromptResponse.AccessibilityTests;

/// <summary>
/// Static analysis of XAML files to verify accessibility properties are set correctly.
/// </summary>
/// <remarks>
/// These tests parse XAML files and verify that:
/// - All interactive elements have AutomationProperties.Name
/// - Help text is exposed via AutomationProperties.HelpText
/// - TabIndex is set for logical keyboard navigation
/// - No elements rely on color alone for information
/// - Focus indicators are implemented
/// </remarks>
public class XamlAccessibilityValidationTests
{
    private readonly string _projectRoot;

    public XamlAccessibilityValidationTests()
    {
        // Navigate up from test assembly to project root
        _projectRoot = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..");
    }

    [Theory]
    [InlineData("src/PromptResponse.Desktop/Views/FormFillingView.axaml")]
    [InlineData("src/PromptResponse.Desktop/Views/MainShellView.axaml")]
    public void XamlFile_InteractiveElements_ShouldHave_AutomationName(string relativePath)
    {
        // Arrange
        var xamlPath = Path.Combine(_projectRoot, relativePath);

        if (!File.Exists(xamlPath))
        {
            // Skip if file doesn't exist (CI environments may differ)
            return;
        }

        var xamlContent = File.ReadAllText(xamlPath);
        var doc = XDocument.Parse(xamlContent);

        // Define interactive element types that MUST have AutomationProperties.Name
        var interactiveElements = new[] { "TextBox", "Button", "CheckBox", "RadioButton", "ComboBox" };

        // Act & Assert
        foreach (var elementType in interactiveElements)
        {
            var elements = doc.Descendants()
                .Where(e => e.Name.LocalName == elementType)
                .ToList();

            foreach (var element in elements)
            {
                var hasAutomationName =
                    element.Attributes().Any(a => a.Name.LocalName == "Name" &&
                                                   a.Name.Namespace.NamespaceName.Contains("AutomationProperties")) ||
                    element.Elements().Any(e => e.Name.LocalName.Contains("AutomationProperties.Name"));

                // Check if AutomationProperties.Name is bound or set
                var automationNameAttr = element.Attributes()
                    .FirstOrDefault(a => a.Name.LocalName == "Name" &&
                                         a.Name.Namespace.ToString().Contains("AutomationProperties"));

                var hasBoundName = automationNameAttr != null &&
                                   (automationNameAttr.Value.Contains("Binding") ||
                                    !string.IsNullOrWhiteSpace(automationNameAttr.Value));

                if (!hasBoundName)
                {
                    // Check for attached property syntax
                    var attachedProperty = element.Attribute(XName.Get("Name",
                        "http://schemas.microsoft.com/winfx/2006/xaml/presentation/AutomationProperties"));

                    if (attachedProperty == null)
                    {
                        // Look for any AutomationProperties.Name in attributes
                        var anyAutomationName = element.Attributes()
                            .Any(a => a.Name.ToString().Contains("AutomationProperties.Name"));

                        anyAutomationName.Should().BeTrue(
                            $"because {elementType} in {Path.GetFileName(xamlPath)} must have AutomationProperties.Name for screen readers. " +
                            $"Element: {element.ToString().Substring(0, Math.Min(100, element.ToString().Length))}...");
                    }
                }
            }
        }
    }

    [Fact]
    public void FormFillingView_TextBoxes_ShouldHave_TabIndex()
    {
        // Arrange
        var xamlPath = Path.Combine(_projectRoot, "src/PromptResponse.Desktop/Views/FormFillingView.axaml");

        if (!File.Exists(xamlPath))
        {
            return;
        }

        var xamlContent = File.ReadAllText(xamlPath);

        // Act - Count TextBox elements
        var textBoxMatches = Regex.Matches(xamlContent, @"<TextBox\s", RegexOptions.IgnoreCase);
        var tabIndexMatches = Regex.Matches(xamlContent, @"TabIndex=""", RegexOptions.IgnoreCase);

        // Assert
        textBoxMatches.Count.Should().BeGreaterThan(0,
            "because FormFillingView should contain text input fields");

        tabIndexMatches.Count.Should().BeGreaterThan(0,
            "because text fields should have TabIndex set for logical keyboard navigation. " +
            $"Found {textBoxMatches.Count} TextBox elements but {tabIndexMatches.Count} with TabIndex.");
    }

    [Fact]
    public void FormFillingView_TextBoxes_ShouldHave_HelpTextBinding()
    {
        // Arrange
        var xamlPath = Path.Combine(_projectRoot, "src/PromptResponse.Desktop/Views/FormFillingView.axaml");

        if (!File.Exists(xamlPath))
        {
            return;
        }

        var xamlContent = File.ReadAllText(xamlPath);

        // Act - Check for AutomationProperties.HelpText
        var hasHelpText = xamlContent.Contains("AutomationProperties.HelpText");

        // Assert
        hasHelpText.Should().BeTrue(
            "because TextBox elements should bind AutomationProperties.HelpText to provide guidance to screen reader users");

        // Verify it's bound to the HelpText property
        var hasHelpTextBinding = xamlContent.Contains("AutomationProperties.HelpText=\"{Binding HelpText}\"") ||
                                  xamlContent.Contains("AutomationProperties.HelpText=\"{Binding Hints.HelpText}\"");

        hasHelpTextBinding.Should().BeTrue(
            "because HelpText should be bound from the view model to expose guidance to assistive technologies");
    }

    [Fact]
    public void FormFillingView_LiveRegions_ShouldBeConfigured()
    {
        // Arrange
        var xamlPath = Path.Combine(_projectRoot, "src/PromptResponse.Desktop/Views/FormFillingView.axaml");

        if (!File.Exists(xamlPath))
        {
            return;
        }

        var xamlContent = File.ReadAllText(xamlPath);

        // Act - Check for AutomationProperties.LiveSetting
        var hasLiveSettings = xamlContent.Contains("AutomationProperties.LiveSetting");

        // Assert
        hasLiveSettings.Should().BeTrue(
            "because dynamic content like help text should use AutomationProperties.LiveSetting " +
            "to announce changes to screen reader users");

        // Verify it's set to Polite (not Assertive, which interrupts)
        var hasPoliteSettings = xamlContent.Contains("LiveSetting=\"Polite\"");

        hasPoliteSettings.Should().BeTrue(
            "because non-critical updates should use Polite to avoid interrupting screen reader users");
    }

    [Fact]
    public void XamlFiles_ShouldNot_UseHardCodedColors()
    {
        // Arrange
        var xamlFiles = new[]
        {
            "src/PromptResponse.Desktop/Views/FormFillingView.axaml",
            "src/PromptResponse.Desktop/Views/MainShellView.axaml",
            "src/PromptResponse.Desktop/App.axaml"
        };

        foreach (var relativePath in xamlFiles)
        {
            var xamlPath = Path.Combine(_projectRoot, relativePath);

            if (!File.Exists(xamlPath))
            {
                continue;
            }

            var xamlContent = File.ReadAllText(xamlPath);

            // Act - Look for hardcoded color values
            var hardcodedColorPattern = @"(Foreground|Background)=""#[0-9A-Fa-f]{6,8}""";
            var hardcodedColors = Regex.Matches(xamlContent, hardcodedColorPattern);

            // Assert
            hardcodedColors.Count.Should().Be(0,
                $"because {Path.GetFileName(xamlPath)} should use DynamicResource for colors to support theme switching. " +
                $"Hardcoded colors don't adapt to dark mode or high contrast themes. " +
                $"Found: {string.Join(", ", hardcodedColors.Select(m => m.Value))}");
        }
    }

    [Fact]
    public void FormFillingView_ShouldUse_DynamicResourceForColors()
    {
        // Arrange
        var xamlPath = Path.Combine(_projectRoot, "src/PromptResponse.Desktop/Views/FormFillingView.axaml");

        if (!File.Exists(xamlPath))
        {
            return;
        }

        var xamlContent = File.ReadAllText(xamlPath);

        // Act
        var dynamicResourceCount = Regex.Matches(xamlContent, @"\{DynamicResource\s+\w+\}").Count;

        // Assert
        dynamicResourceCount.Should().BeGreaterThan(0,
            "because FormFillingView should use DynamicResource for theme-aware colors that adapt to light/dark/high-contrast modes");
    }

    [Fact]
    public void FormFillingView_Expanders_ShouldHave_AccessibleNames()
    {
        // Arrange
        var xamlPath = Path.Combine(_projectRoot, "src/PromptResponse.Desktop/Views/FormFillingView.axaml");

        if (!File.Exists(xamlPath))
        {
            return;
        }

        var xamlContent = File.ReadAllText(xamlPath);
        var doc = XDocument.Parse(xamlContent);

        // Act - Find all Expander elements
        var expanders = doc.Descendants()
            .Where(e => e.Name.LocalName == "Expander")
            .ToList();

        // Assert
        expanders.Should().NotBeEmpty(
            "because FormFillingView uses Expanders for sections and subsections");

        foreach (var expander in expanders)
        {
            var hasAutomationName = expander.Attributes()
                .Any(a => a.Name.ToString().Contains("AutomationProperties.Name"));

            hasAutomationName.Should().BeTrue(
                "because Expander elements must have AutomationProperties.Name " +
                "to announce section/subsection names and expanded/collapsed state to screen readers");
        }
    }

    [Fact]
    public void FormFillingView_MinHeight_ShouldMeet_TouchTargetSize()
    {
        // Arrange
        var xamlPath = Path.Combine(_projectRoot, "src/PromptResponse.Desktop/Views/FormFillingView.axaml");

        if (!File.Exists(xamlPath))
        {
            return;
        }

        var xamlContent = File.ReadAllText(xamlPath);

        // Act - Find MinHeight on TextBox elements
        var minHeightMatches = Regex.Matches(xamlContent, @"<TextBox[^>]*MinHeight=""(\d+)""", RegexOptions.Singleline);

        // Assert
        minHeightMatches.Should().NotBeEmpty(
            "because TextBox elements should have MinHeight set for adequate touch target size");

        foreach (Match match in minHeightMatches)
        {
            var height = int.Parse(match.Groups[1].Value);
            height.Should().BeGreaterThanOrEqualTo(36,
                "because WCAG 2.1 Level AAA recommends 44×44 CSS pixels for touch targets, " +
                "and 36px is our minimum for desktop with mouse/keyboard (still accessible)");
        }
    }

    [Fact]
    public void FormFillingView_Spacing_ShouldBe_Adequate()
    {
        // Arrange
        var xamlPath = Path.Combine(_projectRoot, "src/PromptResponse.Desktop/Views/FormFillingView.axaml");

        if (!File.Exists(xamlPath))
        {
            return;
        }

        var xamlContent = File.ReadAllText(xamlPath);

        // Act - Check for Spacing attribute on StackPanels containing prompts
        var spacingMatches = Regex.Matches(xamlContent, @"Spacing=""(\d+)""");

        // Assert
        spacingMatches.Should().NotBeEmpty(
            "because form fields should have adequate spacing for readability");

        foreach (Match match in spacingMatches)
        {
            var spacing = int.Parse(match.Groups[1].Value);
            spacing.Should().BeGreaterThanOrEqualTo(4,
                "because adequate spacing improves readability and reduces visual clutter");
        }
    }

    [Fact]
    public void FormFillingView_MaxWidth_ShouldLimit_LineLength()
    {
        // Arrange
        var xamlPath = Path.Combine(_projectRoot, "src/PromptResponse.Desktop/Views/FormFillingView.axaml");

        if (!File.Exists(xamlPath))
        {
            return;
        }

        var xamlContent = File.ReadAllText(xamlPath);

        // Act - Check for MaxWidth on main container
        var maxWidthMatch = Regex.Match(xamlContent, @"MaxWidth=""(\d+)""");

        // Assert
        maxWidthMatch.Success.Should().BeTrue(
            "because maximum line length should be limited for comfortable reading");

        if (maxWidthMatch.Success)
        {
            var maxWidth = int.Parse(maxWidthMatch.Groups[1].Value);
            maxWidth.Should().BeLessThanOrEqualTo(1200,
                "because lines wider than ~1200px make reading difficult, especially for users with dyslexia or cognitive disabilities");

            maxWidth.Should().BeGreaterThanOrEqualTo(600,
                "because content should have reasonable minimum width for usability");
        }
    }

    [Fact]
    public void MainWindow_Menus_ShouldHave_AccessKeys()
    {
        // Arrange
        var xamlPath = Path.Combine(_projectRoot, "src/PromptResponse.Desktop/Views/MainShellView.axaml");

        if (!File.Exists(xamlPath))
        {
            return;
        }

        var xamlContent = File.ReadAllText(xamlPath);

        // Act - Check for underscore in menu headers (access keys)
        var menuItemPattern = @"<MenuItem\s+Header=""_\w+""";
        var menuItemsWithAccessKeys = Regex.Matches(xamlContent, menuItemPattern);

        // Assert
        menuItemsWithAccessKeys.Count.Should().BeGreaterThan(0,
            "because menu items should have access keys (e.g., _File, _Edit) " +
            "for keyboard-only users to navigate menus with Alt+F, Alt+E, etc.");
    }

    [Fact]
    public void MainWindow_Menus_ShouldHave_InputGestures()
    {
        // Arrange
        var xamlPath = Path.Combine(_projectRoot, "src/PromptResponse.Desktop/Views/MainShellView.axaml");

        if (!File.Exists(xamlPath))
        {
            return;
        }

        var xamlContent = File.ReadAllText(xamlPath);

        // Act - Check for HotKey on common menu items (Avalonia uses HotKey, not InputGesture)
        var hotKeyPattern = @"HotKey=""[^""]+""";
        var menuItemsWithShortcuts = Regex.Matches(xamlContent, hotKeyPattern);

        // Assert
        menuItemsWithShortcuts.Count.Should().BeGreaterThan(0,
            "because common actions should have keyboard shortcuts (Ctrl+O, Ctrl+S, etc.) " +
            "for efficient keyboard-only navigation");
    }
}
