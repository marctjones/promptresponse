using Avalonia.Media;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// WCAG 2.1 contrast-ratio calculator. Used by tests to enforce theme palette
/// invariants and at runtime to validate any custom theme entered by the user.
/// </summary>
public static class ContrastCalculator
{
    /// <summary>
    /// Computes the WCAG contrast ratio between two colors. Returns a value in [1, 21].
    /// Symmetric: <c>Ratio(a, b) == Ratio(b, a)</c>.
    /// </summary>
    public static double Ratio(Color foreground, Color background)
    {
        var l1 = RelativeLuminance(foreground);
        var l2 = RelativeLuminance(background);
        var (lighter, darker) = l1 > l2 ? (l1, l2) : (l2, l1);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>WCAG AA threshold for normal-size body text: 4.5:1.</summary>
    public const double WcagAANormal = 4.5;

    /// <summary>WCAG AA threshold for large text (≥18pt or ≥14pt bold): 3.0:1.</summary>
    public const double WcagAALarge = 3.0;

    /// <summary>WCAG AAA threshold for normal-size body text: 7.0:1.</summary>
    public const double WcagAAANormal = 7.0;

    /// <summary>WCAG AAA threshold for large text: 4.5:1.</summary>
    public const double WcagAAALarge = 4.5;

    private static double RelativeLuminance(Color color)
    {
        var r = ChannelLuminance(color.R / 255.0);
        var g = ChannelLuminance(color.G / 255.0);
        var b = ChannelLuminance(color.B / 255.0);
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static double ChannelLuminance(double srgb)
    {
        // Inverse sRGB companding — converts the perceptual gamma-encoded channel
        // value into linear-light intensity.
        return srgb <= 0.03928
            ? srgb / 12.92
            : Math.Pow((srgb + 0.055) / 1.055, 2.4);
    }
}
