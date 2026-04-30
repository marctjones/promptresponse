using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using FluentAssertions;
using Xunit;

using AvaloniaStyles = Avalonia.Styling.Styles;

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

    [Fact(Skip = "Design system is being redesigned in Phase 5 (rendering profile system); these tests will be rewritten against the new tokens. Tracked: idlergear task #22.")]
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

    [Fact(Skip = "Design system is being redesigned in Phase 5 (rendering profile system); these tests will be rewritten against the new tokens. Tracked: idlergear task #22.")]
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

    [Fact(Skip = "Design system is being redesigned in Phase 5 (rendering profile system); these tests will be rewritten against the new tokens. Tracked: idlergear task #22.")]
    public void DesignTokens_ContainsSpacingResources()
    {
        // Arrange
        var resourceInclude = new ResourceInclude(new Uri("avares://PromptResponse.Desktop"))
        {
            Source = new Uri("/Styles/DesignTokens.axaml", UriKind.Relative)
        };
        resourceInclude.Loaded.Should().NotBeNull();

        // Act & Assert - Check that key spacing tokens exist
        resourceInclude.TryGetResource("Space2", null, out _).Should().BeTrue("spacing tokens should be defined");
        resourceInclude.TryGetResource("Space4", null, out _).Should().BeTrue("spacing tokens should be defined");
        resourceInclude.TryGetResource("Space6", null, out _).Should().BeTrue("spacing tokens should be defined");
        resourceInclude.TryGetResource("Space8", null, out _).Should().BeTrue("spacing tokens should be defined");
    }

    [Fact(Skip = "Design system is being redesigned in Phase 5 (rendering profile system); these tests will be rewritten against the new tokens. Tracked: idlergear task #22.")]
    public void DesignTokens_ContainsRadiusResources()
    {
        // Arrange
        var resourceInclude = new ResourceInclude(new Uri("avares://PromptResponse.Desktop"))
        {
            Source = new Uri("/Styles/DesignTokens.axaml", UriKind.Relative)
        };
        resourceInclude.Loaded.Should().NotBeNull();

        // Act & Assert - Check that radius tokens exist
        resourceInclude.TryGetResource("RadiusSmall", null, out _).Should().BeTrue("radius tokens should be defined");
        resourceInclude.TryGetResource("RadiusMedium", null, out _).Should().BeTrue("radius tokens should be defined");
        resourceInclude.TryGetResource("RadiusLarge", null, out _).Should().BeTrue("radius tokens should be defined");
    }

    [Fact(Skip = "Design system is being redesigned in Phase 5 (rendering profile system); these tests will be rewritten against the new tokens. Tracked: idlergear task #22.")]
    public void DesignTokens_ContainsFontSizeResources()
    {
        // Arrange
        var resourceInclude = new ResourceInclude(new Uri("avares://PromptResponse.Desktop"))
        {
            Source = new Uri("/Styles/DesignTokens.axaml", UriKind.Relative)
        };
        resourceInclude.Loaded.Should().NotBeNull();

        // Act & Assert - Check that font size tokens exist
        resourceInclude.TryGetResource("FontSizeSmall", null, out _).Should().BeTrue("font size tokens should be defined");
        resourceInclude.TryGetResource("FontSizeBody", null, out _).Should().BeTrue("font size tokens should be defined");
        resourceInclude.TryGetResource("FontSizeTitle", null, out _).Should().BeTrue("font size tokens should be defined");
        resourceInclude.TryGetResource("FontSizeDisplay", null, out _).Should().BeTrue("font size tokens should be defined");
    }

    [Fact(Skip = "Design system is being redesigned in Phase 5 (rendering profile system); these tests will be rewritten against the new tokens. Tracked: idlergear task #22.")]
    public void DesignTokens_ContainsShadowResources()
    {
        // Arrange
        var resourceInclude = new ResourceInclude(new Uri("avares://PromptResponse.Desktop"))
        {
            Source = new Uri("/Styles/DesignTokens.axaml", UriKind.Relative)
        };
        resourceInclude.Loaded.Should().NotBeNull();

        // Act & Assert - Check that shadow tokens exist
        resourceInclude.TryGetResource("ShadowSmall", null, out _).Should().BeTrue("shadow tokens should be defined");
        resourceInclude.TryGetResource("ShadowMedium", null, out _).Should().BeTrue("shadow tokens should be defined");
        resourceInclude.TryGetResource("ShadowLarge", null, out _).Should().BeTrue("shadow tokens should be defined");
    }

    [Fact(Skip = "Design system is being redesigned in Phase 5 (rendering profile system); these tests will be rewritten against the new tokens. Tracked: idlergear task #22.")]
    public void DesignTokens_ContainsColorResources()
    {
        // Arrange
        var resourceInclude = new ResourceInclude(new Uri("avares://PromptResponse.Desktop"))
        {
            Source = new Uri("/Styles/DesignTokens.axaml", UriKind.Relative)
        };
        resourceInclude.Loaded.Should().NotBeNull();

        // Act & Assert - Check that semantic color tokens exist
        resourceInclude.TryGetResource("SuccessBrush", null, out _).Should().BeTrue("semantic color tokens should be defined");
        resourceInclude.TryGetResource("ErrorBrush", null, out _).Should().BeTrue("semantic color tokens should be defined");
        resourceInclude.TryGetResource("WarningBrush", null, out _).Should().BeTrue("semantic color tokens should be defined");
        resourceInclude.TryGetResource("InfoBrush", null, out _).Should().BeTrue("semantic color tokens should be defined");
    }

    [Fact(Skip = "Design system is being redesigned in Phase 5 (rendering profile system); these tests will be rewritten against the new tokens. Tracked: idlergear task #22.")]
    public void ModernControls_ContainsButtonStyles()
    {
        // Arrange
        var styleInclude = new StyleInclude(new Uri("avares://PromptResponse.Desktop"))
        {
            Source = new Uri("/Styles/ModernControls.axaml", UriKind.Relative)
        };

        // Act
        var styles = styleInclude.Loaded as AvaloniaStyles;

        // Assert
        styles.Should().NotBeNull("ModernControls should contain style definitions");
        styles!.Count.Should().BeGreaterThan(0, "ModernControls should define multiple styles");
    }

    [Fact(Skip = "Design system is being redesigned in Phase 5 (rendering profile system); these tests will be rewritten against the new tokens. Tracked: idlergear task #22.")]
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

    [Fact(Skip = "Design system is being redesigned in Phase 5 (rendering profile system); these tests will be rewritten against the new tokens. Tracked: idlergear task #22.")]
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

    [Fact(Skip = "Design system is being redesigned in Phase 5 (rendering profile system); these tests will be rewritten against the new tokens. Tracked: idlergear task #22.")]
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

            var styles = new AvaloniaStyles
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
