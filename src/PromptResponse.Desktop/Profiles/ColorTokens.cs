using Avalonia.Media;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Semantic color slot in the theme palette. Names describe the role, not the hue —
/// the same slot has different concrete colors across <see cref="ColorScheme"/>s.
/// </summary>
public enum ColorRole
{
    /// <summary>The base background of pages and the window.</summary>
    Surface,
    /// <summary>A subtly distinct background used for sidebars, status bars, and chrome.</summary>
    SubtleSurface,
    /// <summary>Raised surface for cards / grouped content. Sits visually above <see cref="Surface"/>.</summary>
    ElevatedSurface,
    /// <summary>Foreground text rendered on <see cref="Surface"/> and <see cref="ElevatedSurface"/>.</summary>
    OnSurface,
    /// <summary>Action-affordance accent color (buttons, links, focus indicators).</summary>
    Primary,
    /// <summary>Foreground text rendered on <see cref="Primary"/>.</summary>
    OnPrimary,
    /// <summary>Soft accent tint used for selection highlights / hover states.</summary>
    Accent,
    /// <summary>Subtle separator / outline color between regions.</summary>
    Border,
    /// <summary>Visible focus indicator. Must contrast strongly with all surfaces.</summary>
    Focus,
    /// <summary>Error / destructive accent.</summary>
    Error,
    /// <summary>Warning / advisory accent.</summary>
    Warning,
    /// <summary>Success / confirmation accent.</summary>
    Success,
    /// <summary>Secondary text (lower visual weight than <see cref="OnSurface"/>).</summary>
    MutedText,
}

/// <summary>
/// The full palette for a single <see cref="ColorScheme"/>: every <see cref="ColorRole"/>
/// mapped to a concrete <see cref="Color"/>.
/// </summary>
public sealed class ColorPalette
{
    private readonly IReadOnlyDictionary<ColorRole, Color> _colors;

    public ColorPalette(ColorScheme scheme, IReadOnlyDictionary<ColorRole, Color> colors)
    {
        Scheme = scheme;
        _colors = colors;
    }

    public ColorScheme Scheme { get; }

    public Color this[ColorRole role] => _colors[role];

    public IEnumerable<ColorRole> Roles => _colors.Keys;
}

/// <summary>
/// Pre-defined palettes for the three color schemes. Tuned for native Windows 11 /
/// macOS aesthetics on Light and Dark, and WCAG AAA on HighContrast. Values are
/// validated by <c>ColorContrastTests</c>.
/// </summary>
/// <remarks>
/// Light/Dark draw from the Windows 11 Fluent and macOS Big Sur+ system palettes:
///   - Surface matches the Mica / window-chrome background.
///   - SubtleSurface is the sidebar / status-bar tint.
///   - ElevatedSurface is the card / popover material.
///   - Primary is the system accent (Win11 blue / macOS controlAccent default).
///   - Border / Focus / MutedText follow native conventions for hairlines and
///     secondary text.
/// HighContrast remains pure black/white pairings with saturated accents — distinct
/// from "Dark", served as a different capability profile.
/// </remarks>
public static class ColorTokens
{
    public static ColorPalette Light { get; } = new(ColorScheme.Light, new Dictionary<ColorRole, Color>
    {
        [ColorRole.Surface]         = Color.FromRgb(0xFA, 0xFA, 0xFA),
        [ColorRole.SubtleSurface]   = Color.FromRgb(0xF2, 0xF2, 0xF2),
        [ColorRole.ElevatedSurface] = Color.FromRgb(0xFF, 0xFF, 0xFF),
        [ColorRole.OnSurface]       = Color.FromRgb(0x1A, 0x1A, 0x1A),
        [ColorRole.MutedText]       = Color.FromRgb(0x52, 0x52, 0x52),
        [ColorRole.Primary]         = Color.FromRgb(0x06, 0x4D, 0xA8),
        [ColorRole.OnPrimary]       = Color.FromRgb(0xFF, 0xFF, 0xFF),
        [ColorRole.Accent]          = Color.FromRgb(0xE5, 0xEE, 0xF8),
        [ColorRole.Border]          = Color.FromRgb(0x4A, 0x4A, 0x4A),
        [ColorRole.Focus]           = Color.FromRgb(0x00, 0x52, 0xCC),
        [ColorRole.Error]           = Color.FromRgb(0xB0, 0x00, 0x20),
        [ColorRole.Warning]         = Color.FromRgb(0x70, 0x44, 0x00),
        [ColorRole.Success]         = Color.FromRgb(0x0A, 0x5A, 0x2A),
    });

    public static ColorPalette Dark { get; } = new(ColorScheme.Dark, new Dictionary<ColorRole, Color>
    {
        [ColorRole.Surface]         = Color.FromRgb(0x1C, 0x1C, 0x1E),
        [ColorRole.SubtleSurface]   = Color.FromRgb(0x24, 0x24, 0x26),
        [ColorRole.ElevatedSurface] = Color.FromRgb(0x2A, 0x2A, 0x2D),
        [ColorRole.OnSurface]       = Color.FromRgb(0xF5, 0xF5, 0xF7),
        [ColorRole.MutedText]       = Color.FromRgb(0xC8, 0xC8, 0xCD),
        [ColorRole.Primary]         = Color.FromRgb(0x6E, 0xB6, 0xFF),
        [ColorRole.OnPrimary]       = Color.FromRgb(0x0A, 0x10, 0x18),
        [ColorRole.Accent]          = Color.FromRgb(0x33, 0x4D, 0x6B),
        [ColorRole.Border]          = Color.FromRgb(0xC8, 0xC8, 0xC8),
        [ColorRole.Focus]           = Color.FromRgb(0xFF, 0xC8, 0x4D),
        [ColorRole.Error]           = Color.FromRgb(0xFF, 0x9B, 0x9B),
        [ColorRole.Warning]         = Color.FromRgb(0xFF, 0xC8, 0x4D),
        [ColorRole.Success]         = Color.FromRgb(0x9C, 0xE6, 0xB1),
    });

    public static ColorPalette HighContrast { get; } = new(ColorScheme.HighContrast, new Dictionary<ColorRole, Color>
    {
        [ColorRole.Surface]         = Color.FromRgb(0x00, 0x00, 0x00),
        [ColorRole.SubtleSurface]   = Color.FromRgb(0x00, 0x00, 0x00),
        [ColorRole.ElevatedSurface] = Color.FromRgb(0x00, 0x00, 0x00),
        [ColorRole.OnSurface]       = Color.FromRgb(0xFF, 0xFF, 0xFF),
        [ColorRole.MutedText]       = Color.FromRgb(0xFF, 0xFF, 0xFF),
        [ColorRole.Primary]         = Color.FromRgb(0xFF, 0xFF, 0x00),
        [ColorRole.OnPrimary]       = Color.FromRgb(0x00, 0x00, 0x00),
        [ColorRole.Accent]          = Color.FromRgb(0x00, 0x00, 0x00),
        [ColorRole.Border]          = Color.FromRgb(0xFF, 0xFF, 0xFF),
        [ColorRole.Focus]           = Color.FromRgb(0xFF, 0xFF, 0x00),
        [ColorRole.Error]           = Color.FromRgb(0xFF, 0x66, 0x66),
        [ColorRole.Warning]         = Color.FromRgb(0xFF, 0xFF, 0x00),
        [ColorRole.Success]         = Color.FromRgb(0x66, 0xFF, 0x66),
    });

    public static ColorPalette For(ColorScheme scheme) => scheme switch
    {
        ColorScheme.Dark => Dark,
        ColorScheme.HighContrast => HighContrast,
        _ => Light,
    };
}
