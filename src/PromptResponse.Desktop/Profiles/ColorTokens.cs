using Avalonia.Media;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Semantic color slot in the theme palette. Names describe the role, not the hue —
/// the same slot has different concrete colors across <see cref="ColorScheme"/>s.
/// </summary>
public enum ColorRole
{
    /// <summary>The base background of pages and major surfaces.</summary>
    Surface,
    /// <summary>Foreground text rendered on <see cref="Surface"/>.</summary>
    OnSurface,
    /// <summary>The primary accent color used for action affordances.</summary>
    Primary,
    /// <summary>Foreground text rendered on <see cref="Primary"/>.</summary>
    OnPrimary,
    /// <summary>Subtle separator / outline color.</summary>
    Border,
    /// <summary>Visible focus indicator (must contrast strongly with both surfaces).</summary>
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
/// Pre-defined palettes for the three color schemes. Tuned for WCAG AA contrast on
/// Light / Dark and WCAG AAA on HighContrast. Values are validated by
/// <c>ColorContrastTests</c>.
/// </summary>
public static class ColorTokens
{
    public static ColorPalette Light { get; } = new(ColorScheme.Light, new Dictionary<ColorRole, Color>
    {
        [ColorRole.Surface] = Color.FromRgb(0xFF, 0xFF, 0xFF),    // pure white
        [ColorRole.OnSurface] = Color.FromRgb(0x1A, 0x1A, 0x1A),  // near-black, AAA on white
        [ColorRole.Primary] = Color.FromRgb(0x06, 0x4D, 0xA8),    // strong blue, AAA on white
        [ColorRole.OnPrimary] = Color.FromRgb(0xFF, 0xFF, 0xFF),  // white on primary
        [ColorRole.Border] = Color.FromRgb(0x4F, 0x4F, 0x4F),     // mid-grey, ≥4.5:1 on white
        [ColorRole.Focus] = Color.FromRgb(0x00, 0x66, 0xCC),      // bright blue focus
        [ColorRole.Error] = Color.FromRgb(0xB0, 0x00, 0x20),      // strong red, AA on white
        [ColorRole.Warning] = Color.FromRgb(0x70, 0x44, 0x00),    // dark amber, AA on white
        [ColorRole.Success] = Color.FromRgb(0x0A, 0x5A, 0x2A),    // dark green, AA on white
        [ColorRole.MutedText] = Color.FromRgb(0x4F, 0x4F, 0x4F),  // grey, AA body text
    });

    public static ColorPalette Dark { get; } = new(ColorScheme.Dark, new Dictionary<ColorRole, Color>
    {
        [ColorRole.Surface] = Color.FromRgb(0x12, 0x12, 0x12),    // near-black surface
        [ColorRole.OnSurface] = Color.FromRgb(0xF5, 0xF5, 0xF5),  // near-white text, AAA on dark
        [ColorRole.Primary] = Color.FromRgb(0x6E, 0xB6, 0xFF),    // light blue, AAA on dark
        [ColorRole.OnPrimary] = Color.FromRgb(0x0A, 0x10, 0x18),  // very dark text on light primary
        [ColorRole.Border] = Color.FromRgb(0xC8, 0xC8, 0xC8),     // light grey, ≥4.5:1 on dark
        [ColorRole.Focus] = Color.FromRgb(0xFF, 0xC8, 0x4D),      // amber focus pops on dark
        [ColorRole.Error] = Color.FromRgb(0xFF, 0x9B, 0x9B),      // light red, AA on dark
        [ColorRole.Warning] = Color.FromRgb(0xFF, 0xC8, 0x4D),    // light amber, AA on dark
        [ColorRole.Success] = Color.FromRgb(0x9C, 0xE6, 0xB1),    // light green, AA on dark
        [ColorRole.MutedText] = Color.FromRgb(0xC8, 0xC8, 0xC8),  // light grey, AA body text
    });

    public static ColorPalette HighContrast { get; } = new(ColorScheme.HighContrast, new Dictionary<ColorRole, Color>
    {
        // Pure black/white pairings deliver 21:1 contrast — well above AAA.
        [ColorRole.Surface] = Color.FromRgb(0x00, 0x00, 0x00),
        [ColorRole.OnSurface] = Color.FromRgb(0xFF, 0xFF, 0xFF),
        [ColorRole.Primary] = Color.FromRgb(0xFF, 0xFF, 0x00),    // saturated yellow on black ≈ 19:1
        [ColorRole.OnPrimary] = Color.FromRgb(0x00, 0x00, 0x00),
        [ColorRole.Border] = Color.FromRgb(0xFF, 0xFF, 0xFF),
        [ColorRole.Focus] = Color.FromRgb(0xFF, 0xFF, 0x00),
        [ColorRole.Error] = Color.FromRgb(0xFF, 0x66, 0x66),      // light red, AAA on black (>7:1)
        [ColorRole.Warning] = Color.FromRgb(0xFF, 0xFF, 0x00),
        [ColorRole.Success] = Color.FromRgb(0x66, 0xFF, 0x66),    // light green, AAA on black
        [ColorRole.MutedText] = Color.FromRgb(0xFF, 0xFF, 0xFF),  // identical to OnSurface — no muted hierarchy in HC
    });

    public static ColorPalette For(ColorScheme scheme) => scheme switch
    {
        ColorScheme.Dark => Dark,
        ColorScheme.HighContrast => HighContrast,
        _ => Light,
    };
}
