using Avalonia.Media;
using AwesomeAssertions;
using PromptResponse.Desktop.Profiles;
using Xunit;

namespace PromptResponse.AccessibilityTests;

/// <summary>
/// Validates every theme palette against WCAG 2.1 contrast requirements. Light/Dark
/// must hit AA (4.5:1 normal / 3.0:1 large); HighContrast must hit AAA (7.0:1 normal /
/// 4.5:1 large) or higher. Failure here blocks merge.
/// </summary>
public class ColorContrastTests
{
    public static IEnumerable<object[]> Palettes()
    {
        yield return new object[] { ColorTokens.Light, ContrastCalculator.WcagAANormal, ContrastCalculator.WcagAALarge };
        yield return new object[] { ColorTokens.Dark, ContrastCalculator.WcagAANormal, ContrastCalculator.WcagAALarge };
        yield return new object[] { ColorTokens.HighContrast, ContrastCalculator.WcagAAANormal, ContrastCalculator.WcagAAALarge };
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void OnSurface_OnSurface_MeetsBodyTextContrast(ColorPalette palette, double normalThreshold, double largeThreshold)
    {
        _ = largeThreshold;
        var ratio = ContrastCalculator.Ratio(palette[ColorRole.OnSurface], palette[ColorRole.Surface]);

        ratio.Should().BeGreaterThanOrEqualTo(normalThreshold,
            $"body text on Surface in {palette.Scheme} must meet body-text contrast threshold");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void OnPrimary_OnPrimary_MeetsBodyTextContrast(ColorPalette palette, double normalThreshold, double largeThreshold)
    {
        _ = largeThreshold;
        var ratio = ContrastCalculator.Ratio(palette[ColorRole.OnPrimary], palette[ColorRole.Primary]);

        ratio.Should().BeGreaterThanOrEqualTo(normalThreshold,
            $"text on Primary in {palette.Scheme} must meet body-text contrast threshold");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Primary_OnSurface_MeetsLargeTextContrast(ColorPalette palette, double normalThreshold, double largeThreshold)
    {
        _ = normalThreshold;
        // Primary is often used as a button background or large-text accent on the
        // surface — it needs to be visible from the surface, even if not body-text usage.
        var ratio = ContrastCalculator.Ratio(palette[ColorRole.Primary], palette[ColorRole.Surface]);

        ratio.Should().BeGreaterThanOrEqualTo(largeThreshold,
            $"Primary on Surface in {palette.Scheme} must meet large-text contrast threshold");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Border_OnSurface_MeetsLargeTextContrast(ColorPalette palette, double normalThreshold, double largeThreshold)
    {
        _ = normalThreshold;
        var ratio = ContrastCalculator.Ratio(palette[ColorRole.Border], palette[ColorRole.Surface]);

        ratio.Should().BeGreaterThanOrEqualTo(largeThreshold,
            $"Border on Surface in {palette.Scheme} must be visible (large-text contrast)");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Focus_OnSurface_MeetsLargeTextContrast(ColorPalette palette, double normalThreshold, double largeThreshold)
    {
        _ = normalThreshold;
        var ratio = ContrastCalculator.Ratio(palette[ColorRole.Focus], palette[ColorRole.Surface]);

        ratio.Should().BeGreaterThanOrEqualTo(largeThreshold,
            $"Focus indicator on Surface in {palette.Scheme} must be unambiguously visible");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Error_OnSurface_MeetsBodyTextContrast(ColorPalette palette, double normalThreshold, double largeThreshold)
    {
        _ = largeThreshold;
        var ratio = ContrastCalculator.Ratio(palette[ColorRole.Error], palette[ColorRole.Surface]);

        ratio.Should().BeGreaterThanOrEqualTo(normalThreshold,
            $"Error text on Surface in {palette.Scheme} must meet body-text contrast threshold");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Success_OnSurface_MeetsLargeTextContrast(ColorPalette palette, double normalThreshold, double largeThreshold)
    {
        _ = normalThreshold;
        var ratio = ContrastCalculator.Ratio(palette[ColorRole.Success], palette[ColorRole.Surface]);

        ratio.Should().BeGreaterThanOrEqualTo(largeThreshold,
            $"Success accent on Surface in {palette.Scheme} must meet large-text contrast threshold");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void MutedText_OnSurface_MeetsBodyTextContrast(ColorPalette palette, double normalThreshold, double largeThreshold)
    {
        _ = largeThreshold;
        var ratio = ContrastCalculator.Ratio(palette[ColorRole.MutedText], palette[ColorRole.Surface]);

        ratio.Should().BeGreaterThanOrEqualTo(normalThreshold,
            $"MutedText on Surface in {palette.Scheme} must meet body-text contrast threshold");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void OnSurface_OnSubtleSurface_MeetsBodyTextContrast(ColorPalette palette, double normalThreshold, double largeThreshold)
    {
        _ = largeThreshold;
        // Body text rendered against the sidebar / status-bar tint must remain readable.
        var ratio = ContrastCalculator.Ratio(palette[ColorRole.OnSurface], palette[ColorRole.SubtleSurface]);

        ratio.Should().BeGreaterThanOrEqualTo(normalThreshold,
            $"OnSurface on SubtleSurface in {palette.Scheme} must meet body-text contrast threshold");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void OnSurface_OnElevatedSurface_MeetsBodyTextContrast(ColorPalette palette, double normalThreshold, double largeThreshold)
    {
        _ = largeThreshold;
        // Body text rendered on cards / dialogs must remain readable.
        var ratio = ContrastCalculator.Ratio(palette[ColorRole.OnSurface], palette[ColorRole.ElevatedSurface]);

        ratio.Should().BeGreaterThanOrEqualTo(normalThreshold,
            $"OnSurface on ElevatedSurface in {palette.Scheme} must meet body-text contrast threshold");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void EveryRoleIsPresent(ColorPalette palette, double normalThreshold, double largeThreshold)
    {
        _ = normalThreshold;
        _ = largeThreshold;
        // Every palette must define every role so the renderer can swap palettes
        // without conditional checks. Catches unintended omissions.
        var allRoles = Enum.GetValues<ColorRole>();
        foreach (var role in allRoles)
        {
            palette.Roles.Should().Contain(role, $"{palette.Scheme} palette is missing {role}");
        }
    }

    [Fact]
    public void HighContrast_AllPairings_AreAtLeastAAALevel()
    {
        // Belt-and-braces: Pure HighContrast pairings should be 21:1 (black/white).
        // The accent colors (yellow, light red, light green) need to exceed AAA on black.
        var hc = ColorTokens.HighContrast;
        ContrastCalculator.Ratio(hc[ColorRole.OnSurface], hc[ColorRole.Surface])
            .Should().BeGreaterThanOrEqualTo(15.0, "white-on-black is the strongest pairing");
        ContrastCalculator.Ratio(hc[ColorRole.OnPrimary], hc[ColorRole.Primary])
            .Should().BeGreaterThanOrEqualTo(ContrastCalculator.WcagAAANormal);
    }
}

public class ContrastCalculatorTests
{
    [Fact]
    public void Ratio_BlackOnWhite_Is21To1()
    {
        var ratio = ContrastCalculator.Ratio(Color.FromRgb(0, 0, 0), Color.FromRgb(255, 255, 255));

        ratio.Should().BeApproximately(21.0, 0.01);
    }

    [Fact]
    public void Ratio_IdenticalColors_Is1To1()
    {
        var ratio = ContrastCalculator.Ratio(Color.FromRgb(0x88, 0x88, 0x88), Color.FromRgb(0x88, 0x88, 0x88));

        ratio.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public void Ratio_IsSymmetric()
    {
        var fg = Color.FromRgb(0x10, 0x20, 0x30);
        var bg = Color.FromRgb(0xE0, 0xE0, 0xE0);

        var ab = ContrastCalculator.Ratio(fg, bg);
        var ba = ContrastCalculator.Ratio(bg, fg);

        ab.Should().BeApproximately(ba, 0.0001);
    }

    [Fact]
    public void Thresholds_MatchWcagSpec()
    {
        ContrastCalculator.WcagAANormal.Should().Be(4.5);
        ContrastCalculator.WcagAALarge.Should().Be(3.0);
        ContrastCalculator.WcagAAANormal.Should().Be(7.0);
        ContrastCalculator.WcagAAALarge.Should().Be(4.5);
    }
}
