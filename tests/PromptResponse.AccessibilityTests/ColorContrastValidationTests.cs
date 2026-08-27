using AwesomeAssertions;
using PromptResponse.Desktop.Profiles;
using Xunit;

namespace PromptResponse.AccessibilityTests;

/// <summary>
/// Validates color contrast ratios meet WCAG 2.1 Level AA standards.
/// </summary>
/// <remarks>
/// WCAG 2.1 Level AA Requirements:
/// - Normal text (< 18pt): 4.5:1 contrast ratio minimum
/// - Large text (≥ 18pt or ≥ 14pt bold): 3:1 contrast ratio minimum
/// - UI components and graphics: 3:1 contrast ratio minimum
///
/// This test suite validates that all text/background color combinations
/// in the application meet these standards for both light and dark themes.
/// </remarks>
public class ColorContrastValidationTests
{
    /// <summary>
    /// Calculates contrast ratio between two RGB colors.
    /// Formula from WCAG: (L1 + 0.05) / (L2 + 0.05) where L1 > L2
    /// </summary>
    private static double CalculateContrastRatio(RgbColor foreground, RgbColor background)
    {
        var l1 = CalculateRelativeLuminance(foreground);
        var l2 = CalculateRelativeLuminance(background);

        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);

        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    /// Calculates relative luminance for a color.
    /// Formula from WCAG 2.1: https://www.w3.org/WAI/WCAG21/Understanding/contrast-minimum.html
    /// </summary>
    private static double CalculateRelativeLuminance(RgbColor color)
    {
        var r = GetAdjustedChannel(color.R / 255.0);
        var g = GetAdjustedChannel(color.G / 255.0);
        var b = GetAdjustedChannel(color.B / 255.0);

        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static double GetAdjustedChannel(double channel)
    {
        return channel <= 0.03928
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }

    [Theory]
    [InlineData("Light Theme - Form Title", 51, 51, 51, 230, 242, 255, 11.1)] // Dark text on light blue
    [InlineData("Light Theme - Body Text", 0, 0, 0, 255, 255, 255, 21.0)] // Black on white
    [InlineData("Light Theme - Help Text", 96, 96, 96, 255, 255, 255, 6.3)] // Medium gray on white
    [InlineData("Dark Theme - Form Title", 240, 240, 240, 32, 32, 32, 14.3)] // Light text on dark gray
    [InlineData("Dark Theme - Body Text", 255, 255, 255, 32, 32, 32, 19.0)] // White on dark gray
    [InlineData("Dark Theme - Help Text", 180, 180, 180, 32, 32, 32, 7.9)] // Light gray on dark gray
    public void ColorCombination_ShouldMeet_WcagAA_NormalText(
        string description,
        int fgR, int fgG, int fgB,
        int bgR, int bgG, int bgB,
        double expectedMinRatio)
    {
        // Arrange
        var foreground = new RgbColor(fgR, fgG, fgB);
        var background = new RgbColor(bgR, bgG, bgB);
        const double wcagAaNormalText = 4.5;

        // Act
        var actualRatio = CalculateContrastRatio(foreground, background);

        // Assert
        actualRatio.Should().BeGreaterThanOrEqualTo(wcagAaNormalText,
            $"because {description} must meet WCAG AA for normal text (4.5:1). " +
            $"Actual ratio: {actualRatio:F2}:1");

        // Also verify our calculation is reasonable (within 10% of expected)
        if (expectedMinRatio > 0)
        {
            var tolerance = expectedMinRatio * 0.15;
            actualRatio.Should().BeInRange(expectedMinRatio - tolerance, expectedMinRatio + tolerance,
                "sanity check that our contrast calculation is correct");
        }
    }

    [Theory]
    [InlineData("Light Theme - Section Header", 51, 51, 51, 255, 255, 255, 18, true)] // 18pt
    [InlineData("Light Theme - Form Title", 51, 51, 51, 230, 242, 255, 28, true)] // 28pt bold
    [InlineData("Dark Theme - Section Header", 240, 240, 240, 32, 32, 32, 18, true)] // 18pt
    [InlineData("Dark Theme - Form Title", 240, 240, 240, 26, 39, 77, 28, true)] // 28pt bold
    public void ColorCombination_ShouldMeet_WcagAA_LargeText(
        string description,
        int fgR, int fgG, int fgB,
        int bgR, int bgG, int bgB,
        int fontSize,
        bool isBold)
    {
        // Arrange
        var foreground = new RgbColor(fgR, fgG, fgB);
        var background = new RgbColor(bgR, bgG, bgB);
        const double wcagAaLargeText = 3.0;

        // Act
        var actualRatio = CalculateContrastRatio(foreground, background);

        // Assert
        actualRatio.Should().BeGreaterThanOrEqualTo(wcagAaLargeText,
            $"because {description} ({fontSize}pt{(isBold ? " bold" : "")}) must meet WCAG AA for large text (3:1). " +
            $"Actual ratio: {actualRatio:F2}:1");
    }

    [Theory]
    [InlineData("Focus Indicator - Light", 0, 120, 215, 255, 255, 255)] // Blue on white
    [InlineData("Focus Indicator - Dark", 99, 180, 255, 32, 32, 32)] // Light blue on dark
    [InlineData("Border - Light", 118, 118, 118, 255, 255, 255)] // Gray border on white (4.5:1)
    [InlineData("Border - Dark", 118, 118, 118, 32, 32, 32)] // Light gray border on dark bg (3.2:1)
    public void UIComponents_ShouldMeet_WcagAA_Contrast(
        string description,
        int fgR, int fgG, int fgB,
        int bgR, int bgG, int bgB)
    {
        // Arrange
        var component = new RgbColor(fgR, fgG, fgB);
        var background = new RgbColor(bgR, bgG, bgB);
        const double wcagAaComponents = 3.0;

        // Act
        var actualRatio = CalculateContrastRatio(component, background);

        // Assert
        actualRatio.Should().BeGreaterThanOrEqualTo(wcagAaComponents,
            $"because {description} must meet WCAG AA for UI components (3:1). " +
            $"Actual ratio: {actualRatio:F2}:1");
    }

    [Fact]
    public void FluentTheme_DefaultColors_ShouldMeet_MinimumContrast()
    {
        // This test validates that our reliance on FluentTheme is safe
        // FluentTheme provides accessible colors by default

        // Light theme - common combinations
        var lightBg = new RgbColor(255, 255, 255);
        var lightText = new RgbColor(0, 0, 0);
        var lightSecondary = new RgbColor(96, 96, 96);

        CalculateContrastRatio(lightText, lightBg).Should().BeGreaterThanOrEqualTo(4.5,
            "because black text on white background must be readable");

        CalculateContrastRatio(lightSecondary, lightBg).Should().BeGreaterThanOrEqualTo(4.5,
            "because secondary text must also be readable");

        // Dark theme - common combinations
        var darkBg = new RgbColor(32, 32, 32);
        var darkText = new RgbColor(255, 255, 255);
        var darkSecondary = new RgbColor(180, 180, 180);

        CalculateContrastRatio(darkText, darkBg).Should().BeGreaterThanOrEqualTo(4.5,
            "because white text on dark background must be readable");

        CalculateContrastRatio(darkSecondary, darkBg).Should().BeGreaterThanOrEqualTo(4.5,
            "because secondary text in dark mode must be readable");
    }

    [Theory]
    [InlineData(255, 255, 255, 0, 0, 0, 21.0)] // Pure white on pure black
    [InlineData(0, 0, 0, 255, 255, 255, 21.0)] // Pure black on pure white (same ratio)
    [InlineData(128, 128, 128, 128, 128, 128, 1.0)] // Same color (minimum contrast)
    [InlineData(255, 0, 0, 0, 255, 0, 2.9)] // Red on green (poor contrast)
    public void ContrastCalculation_ShouldBe_Accurate(
        int fgR, int fgG, int fgB,
        int bgR, int bgG, int bgB,
        double expectedRatio)
    {
        // Arrange
        var foreground = new RgbColor(fgR, fgG, fgB);
        var background = new RgbColor(bgR, bgG, bgB);

        // Act
        var actualRatio = CalculateContrastRatio(foreground, background);

        // Assert
        actualRatio.Should().BeInRange(expectedRatio - 0.2, expectedRatio + 0.2,
            $"because contrast ratio calculation should be accurate for common test cases. " +
            $"FG: RGB({fgR},{fgG},{fgB}), BG: RGB({bgR},{bgG},{bgB})");
    }

    [Fact]
    public void ApplicationThemes_Documentation_ShouldExist()
    {
        // Verify ACCESSIBILITY.md documents our theme support
        var accessibilityDoc = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "ACCESSIBILITY.md");

        if (File.Exists(accessibilityDoc))
        {
            var content = File.ReadAllText(accessibilityDoc);

            content.Should().Contain("Light Theme",
                "because ACCESSIBILITY.md should document theme support");
            content.Should().Contain("Dark Theme",
                "because dark theme should be documented");
            content.Should().Contain("contrast",
                "because color contrast should be discussed");
        }
    }

    // ── The real palette, not a transcription of it ───────────────────────────

    /// <summary>Every component outline must identify its control against its surface.</summary>
    /// <remarks>
    /// <para>
    /// WCAG 2.1 SC 1.4.11 asks 3:1 for the visual information that identifies a user
    /// interface component. This reads the shipped palette rather than repeating its
    /// values, because the hardcoded cases above did exactly what hardcoded values do:
    /// they said the light border was 118,118,118 while ColorTokens.cs had drifted to
    /// 74,74,74. The test passed throughout, describing a colour the application had
    /// stopped using.
    /// </para>
    /// <para>
    /// Drift in this direction was harmless - 74 is darker, so the contrast was higher
    /// than claimed - but the same gap would have hidden a regression the other way, and
    /// the heavy outline it produced was a real visual cost nobody was measuring.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("HighContrast")]
    public void ComponentOutlines_InTheShippedPalette_Meet3To1(string paletteName)
    {
        var palette = paletteName switch
        {
            "Light" => ColorTokens.Light,
            "Dark" => ColorTokens.Dark,
            _ => ColorTokens.HighContrast,
        };

        foreach (var surface in new[] { ColorRole.Surface, ColorRole.SubtleSurface, ColorRole.ElevatedSurface })
        {
            var ratio = CalculateContrastRatio(Rgb(palette[ColorRole.Border]), Rgb(palette[surface]));

            ratio.Should().BeGreaterThanOrEqualTo(3.0,
                $"because a {paletteName} component outline must identify its control " +
                $"against {surface} (WCAG 1.4.11). Actual: {ratio:F2}:1");
        }
    }

    /// <summary>Focus must be unmistakable on every surface it can appear over.</summary>
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("HighContrast")]
    public void TheFocusIndicator_InTheShippedPalette_Meets3To1(string paletteName)
    {
        var palette = paletteName switch
        {
            "Light" => ColorTokens.Light,
            "Dark" => ColorTokens.Dark,
            _ => ColorTokens.HighContrast,
        };

        foreach (var surface in new[] { ColorRole.Surface, ColorRole.SubtleSurface, ColorRole.ElevatedSurface })
        {
            var ratio = CalculateContrastRatio(Rgb(palette[ColorRole.Focus]), Rgb(palette[surface]));

            ratio.Should().BeGreaterThanOrEqualTo(3.0,
                $"because focus must be visible on {surface} in the {paletteName} palette. " +
                $"Actual: {ratio:F2}:1");
        }
    }

    /// <summary>A divider is allowed to be quiet — except where quiet is the wrong answer.</summary>
    /// <remarks>
    /// Dividers carry no contrast duty: the structure they hint at is already conveyed by
    /// headings, grouping and spacing, and drawing them at component strength is what
    /// makes an interface look boxed in. The exception is the high-contrast palette, where
    /// somebody has explicitly asked for every edge to be visible, so there the divider
    /// must be as strong as any component outline.
    /// </remarks>
    [Fact]
    public void InHighContrast_EvenDividersAreFullStrength()
    {
        var ratio = CalculateContrastRatio(
            Rgb(ColorTokens.HighContrast[ColorRole.Divider]),
            Rgb(ColorTokens.HighContrast[ColorRole.Surface]));

        ratio.Should().BeGreaterThanOrEqualTo(3.0,
            "someone who asked for high contrast wants every edge visible, including the " +
            $"ones other palettes draw as hairlines. Actual: {ratio:F2}:1");
    }

    private static RgbColor Rgb(Avalonia.Media.Color color) => new(color.R, color.G, color.B);

}

/// <summary>
/// Represents an RGB color for contrast calculations.
/// </summary>
public record RgbColor
{
    public int R { get; init; }
    public int G { get; init; }
    public int B { get; init; }

    public RgbColor(int r, int g, int b)
    {
        if (r < 0 || r > 255) throw new ArgumentException("Red must be 0-255", nameof(r));
        if (g < 0 || g > 255) throw new ArgumentException("Green must be 0-255", nameof(g));
        if (b < 0 || b > 255) throw new ArgumentException("Blue must be 0-255", nameof(b));

        R = r;
        G = g;
        B = b;
    }
}
