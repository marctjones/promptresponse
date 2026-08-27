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
    /// <summary>
    /// Outline of an interactive component: a text box, a button, a combo box.
    /// </summary>
    /// <remarks>
    /// WCAG 2.1 asks 3:1 for the visual information that identifies a component
    /// (1.4.11), not the 4.5:1 that text needs. The light palette used to sit at 8.6:1,
    /// far past the requirement, and the result was a heavy outline around everything
    /// that read as a wireframe rather than an application. It now sits near 4.5:1 -
    /// comfortably above the bar, and quiet enough that structure comes from spacing and
    /// type rather than from boxes.
    /// </remarks>
    Border,

    /// <summary>
    /// A hairline between regions: panel edges, list separators, table rules.
    /// </summary>
    /// <remarks>
    /// Not a component boundary, so no contrast minimum applies: the structure it hints
    /// at is already carried by headings, spacing and grouping. Drawing these at
    /// component strength is what makes an interface look boxed-in. The high-contrast
    /// palette deliberately collapses this back onto <see cref="Border"/>, because a
    /// user who asked for high contrast wants every edge visible.
    /// </remarks>
    Divider,
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
        [ColorRole.Border]          = Color.FromRgb(0x76, 0x76, 0x76),
        [ColorRole.Divider]         = Color.FromRgb(0xDD, 0xDD, 0xDD),
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
        [ColorRole.Border]          = Color.FromRgb(0x8A, 0x8A, 0x90),
        [ColorRole.Divider]         = Color.FromRgb(0x3A, 0x3A, 0x3E),
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
        // Collapsed onto Border on purpose: someone who asked for high contrast wants
        // every edge visible, including the ones other palettes draw as hairlines.
        [ColorRole.Divider]         = Color.FromRgb(0xFF, 0xFF, 0xFF),
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
