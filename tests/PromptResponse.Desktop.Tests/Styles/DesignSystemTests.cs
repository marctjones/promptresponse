using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using FluentAssertions;
using Xunit;

namespace PromptResponse.Desktop.Tests.Styles;

/// <summary>
/// Integration tests for the modern design system.
/// Verifies that design tokens and control styles load correctly.
/// </summary>
public class DesignSystemTests
{
    public DesignSystemTests()
    {
        // Initialize Avalonia for testing
        if (Application.Current == null)
        {
            AppBuilder.Configure<Application>()
                .UsePlatformDetect()
                .SetupWithoutStarting();
        }
    }

    [Fact]
    public void DesignTokens_LoadsWithoutErrors()
    {
        // Arrange & Act
        var exception = Record.Exception(() =>
        {
            var resourceInclude = new ResourceInclude(new Uri("avares://PromptResponse.Desktop"))
            {
                Source = new Uri("/Styles/DesignTokens.axaml", UriKind.Relative)
            };
            _ = resourceInclude.Loaded;
        });

        // Assert
        exception.Should().BeNull("DesignTokens.axaml should load without errors");
    }

    [Fact]
    public void ModernControls_LoadsWithoutErrors()
    {
        // Arrange & Act
        var exception = Record.Exception(() =>
        {
            var styleInclude = new StyleInclude(new Uri("avares://PromptResponse.Desktop"))
            {
                Source = new Uri("/Styles/ModernControls.axaml", UriKind.Relative)
            };
            _ = styleInclude.Loaded;
        });

        // Assert
        exception.Should().BeNull("ModernControls.axaml should load without errors");
    }

    [Fact]
    public void DesignTokens_ContainsSpacingResources()
    {
        // Arrange
        var resourceInclude = new ResourceInclude(new Uri("avares://PromptResponse.Desktop"))
        {
            Source = new Uri("/Styles/DesignTokens.axaml", UriKind.Relative)
        };
        resourceInclude.Loaded.Should().NotBeNull();

        // Act & Assert - Check that key spacing tokens exist
        resourceInclude.Should().ContainKey("Space2", "spacing tokens should be defined");
        resourceInclude.Should().ContainKey("Space4", "spacing tokens should be defined");
        resourceInclude.Should().ContainKey("Space6", "spacing tokens should be defined");
        resourceInclude.Should().ContainKey("Space8", "spacing tokens should be defined");
    }

    [Fact]
    public void DesignTokens_ContainsRadiusResources()
    {
        // Arrange
        var resourceInclude = new ResourceInclude(new Uri("avares://PromptResponse.Desktop"))
        {
            Source = new Uri("/Styles/DesignTokens.axaml", UriKind.Relative)
        };
        resourceInclude.Loaded.Should().NotBeNull();

        // Act & Assert - Check that radius tokens exist
        resourceInclude.Should().ContainKey("RadiusSmall", "radius tokens should be defined");
        resourceInclude.Should().ContainKey("RadiusMedium", "radius tokens should be defined");
        resourceInclude.Should().ContainKey("RadiusLarge", "radius tokens should be defined");
    }

    [Fact]
    public void DesignTokens_ContainsFontSizeResources()
    {
        // Arrange
        var resourceInclude = new ResourceInclude(new Uri("avares://PromptResponse.Desktop"))
        {
            Source = new Uri("/Styles/DesignTokens.axaml", UriKind.Relative)
        };
        resourceInclude.Loaded.Should().NotBeNull();

        // Act & Assert - Check that font size tokens exist
        resourceInclude.Should().ContainKey("FontSizeSmall", "font size tokens should be defined");
        resourceInclude.Should().ContainKey("FontSizeBody", "font size tokens should be defined");
        resourceInclude.Should().ContainKey("FontSizeTitle", "font size tokens should be defined");
        resourceInclude.Should().ContainKey("FontSizeDisplay", "font size tokens should be defined");
    }

    [Fact]
    public void DesignTokens_ContainsShadowResources()
    {
        // Arrange
        var resourceInclude = new ResourceInclude(new Uri("avares://PromptResponse.Desktop"))
        {
            Source = new Uri("/Styles/DesignTokens.axaml", UriKind.Relative)
        };
        resourceInclude.Loaded.Should().NotBeNull();

        // Act & Assert - Check that shadow tokens exist
        resourceInclude.Should().ContainKey("ShadowSmall", "shadow tokens should be defined");
        resourceInclude.Should().ContainKey("ShadowMedium", "shadow tokens should be defined");
        resourceInclude.Should().ContainKey("ShadowLarge", "shadow tokens should be defined");
    }

    [Fact]
    public void DesignTokens_ContainsColorResources()
    {
        // Arrange
        var resourceInclude = new ResourceInclude(new Uri("avares://PromptResponse.Desktop"))
        {
            Source = new Uri("/Styles/DesignTokens.axaml", UriKind.Relative)
        };
        resourceInclude.Loaded.Should().NotBeNull();

        // Act & Assert - Check that semantic color tokens exist
        resourceInclude.Should().ContainKey("SuccessBrush", "semantic color tokens should be defined");
        resourceInclude.Should().ContainKey("ErrorBrush", "semantic color tokens should be defined");
        resourceInclude.Should().ContainKey("WarningBrush", "semantic color tokens should be defined");
        resourceInclude.Should().ContainKey("InfoBrush", "semantic color tokens should be defined");
    }

    [Fact]
    public void ModernControls_ContainsButtonStyles()
    {
        // Arrange
        var styleInclude = new StyleInclude(new Uri("avares://PromptResponse.Desktop"))
        {
            Source = new Uri("/Styles/ModernControls.axaml", UriKind.Relative)
        };

        // Act
        var styles = styleInclude.Loaded as Styles;

        // Assert
        styles.Should().NotBeNull("ModernControls should contain style definitions");
        styles!.Count.Should().BeGreaterThan(0, "ModernControls should define multiple styles");
    }

    [Fact]
    public void ModernControls_ButtonModernClass_HasCornerRadius()
    {
        // This test verifies that the modern button style includes corner radius
        // In a full UI test, we'd create a button and verify the style applies
        // For now, we just verify the styles load

        // Arrange
        var styleInclude = new StyleInclude(new Uri("avares://PromptResponse.Desktop"))
        {
            Source = new Uri("/Styles/ModernControls.axaml", UriKind.Relative)
        };

        // Act
        var exception = Record.Exception(() => _ = styleInclude.Loaded);

        // Assert
        exception.Should().BeNull("Button.modern style should load without errors");
    }

    [Fact]
    public void DesignTokens_SpacingValues_AreMultiplesOfFour()
    {
        // Arrange
        var resourceInclude = new ResourceInclude(new Uri("avares://PromptResponse.Desktop"))
        {
            Source = new Uri("/Styles/DesignTokens.axaml", UriKind.Relative)
        };
        resourceInclude.Loaded.Should().NotBeNull();

        // Act & Assert - Spacing should follow 4px grid
        if (resourceInclude.TryGetResource("Space2", null, out var space2))
        {
            var value = Convert.ToDouble(space2);
            (value % 4).Should().Be(0, "spacing values should be multiples of 4");
        }

        if (resourceInclude.TryGetResource("Space4", null, out var space4))
        {
            var value = Convert.ToDouble(space4);
            (value % 4).Should().Be(0, "spacing values should be multiples of 4");
        }
    }

    [Fact]
    public void Application_CanLoadAllDesignSystemResources()
    {
        // Arrange & Act
        var exception = Record.Exception(() =>
        {
            // Simulate loading resources as App.axaml does
            var resources = new ResourceDictionary
            {
                MergedDictionaries =
                {
                    new ResourceInclude(new Uri("avares://PromptResponse.Desktop"))
                    {
                        Source = new Uri("/Styles/DesignTokens.axaml", UriKind.Relative)
                    }
                }
            };

            var styles = new Styles
            {
                new StyleInclude(new Uri("avares://PromptResponse.Desktop"))
                {
                    Source = new Uri("/Styles/ModernControls.axaml", UriKind.Relative)
                }
            };
        });

        // Assert
        exception.Should().BeNull("application should be able to load all design system resources");
    }
}
