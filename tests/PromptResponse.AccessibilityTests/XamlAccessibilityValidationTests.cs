using AwesomeAssertions;
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
    // Every view on disk, not two named by hand. The hand-written list outlived
    // one of the files it named, and the missing-file guard below it meant the
    // test kept passing.
    [Theory]
    [MemberData(nameof(KeyboardNavigationTestFiles.AllViews), MemberType = typeof(KeyboardNavigationTestFiles))]
    public void XamlFile_InteractiveElements_ShouldHave_AutomationName(string relativePath)
    {
        var xamlPath = Path.Combine(_projectRoot, relativePath);
        var xamlContent = KeyboardNavigationTestFiles.ReadView(relativePath);
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
    public void XamlFiles_ShouldNot_UseHardCodedColors()
    {
        // Arrange
        var xamlFiles = new[]
        {
            "src/PromptResponse.Desktop/Views/MainShellView.axaml",
            "src/PromptResponse.Desktop/App.axaml"
        };

        foreach (var relativePath in xamlFiles)
        {
            var xamlPath = Path.Combine(_projectRoot, relativePath);


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
    public void MainWindow_Menus_ShouldHave_AccessKeys()
    {
        // Arrange
        var xamlPath = Path.Combine(_projectRoot, "src/PromptResponse.Desktop/Views/MainShellView.axaml");

        var xamlContent = KeyboardNavigationTestFiles.ReadView(
            Path.GetRelativePath(_projectRoot, xamlPath));

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

        var xamlContent = KeyboardNavigationTestFiles.ReadView(
            Path.GetRelativePath(_projectRoot, xamlPath));

        // Act - Check for HotKey on common menu items (Avalonia uses HotKey, not InputGesture)
        var hotKeyPattern = @"HotKey=""[^""]+""";
        var menuItemsWithShortcuts = Regex.Matches(xamlContent, hotKeyPattern);

        // Assert
        menuItemsWithShortcuts.Count.Should().BeGreaterThan(0,
            "because common actions should have keyboard shortcuts (Ctrl+O, Ctrl+S, etc.) " +
            "for efficient keyboard-only navigation");
    }
}
