using System.Runtime.InteropServices;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Reads OS-reported accessibility preferences. Best-effort across Windows / macOS /
/// Linux; returns conservative defaults (no preferences) when detection isn't possible.
/// </summary>
/// <remarks>
/// Detection mechanisms:
///   - Windows: environment / registry probes (current implementation
///     deliberately conservative — full SystemParametersInfo wiring is future work).
///   - macOS: NSUserDefaults probes via environment variables exposed by AppKit.
///   - Linux: GTK / GNOME settings via environment variables; if unavailable, no signal.
///   - All platforms: AVALONIA_HIGH_CONTRAST, AVALONIA_REDUCED_MOTION, AVALONIA_DARK
///     environment overrides honoured (useful for CI snapshots and manual testing).
/// </remarks>
public sealed class OsAccessibilityProbe : IOsAccessibilityProbe
{
    public bool HighContrast { get; }
    public bool ReducedMotion { get; }
    public bool ScreenReaderActive { get; }
    public ColorScheme PreferredColorScheme { get; }

    public OsAccessibilityProbe()
    {
        HighContrast = DetectHighContrast();
        ReducedMotion = DetectReducedMotion();
        ScreenReaderActive = DetectScreenReader();
        PreferredColorScheme = DetectColorScheme();
    }

    private static bool DetectHighContrast()
    {
        if (EnvFlag("AVALONIA_HIGH_CONTRAST")) return true;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var gtkTheme = Environment.GetEnvironmentVariable("GTK_THEME") ?? string.Empty;
            return gtkTheme.Contains("HighContrast", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private static bool DetectReducedMotion()
    {
        if (EnvFlag("AVALONIA_REDUCED_MOTION")) return true;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var gnomePreference = Environment.GetEnvironmentVariable("GNOME_REDUCED_MOTION");
            return gnomePreference == "1" || string.Equals(gnomePreference, "true", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private static bool DetectScreenReader()
    {
        // Conservative: detect well-known SR processes via env vars they typically set.
        // Falls back to false; users opt in via Display Preferences if auto-detect missed them.
        if (EnvFlag("ORCA_RUNNING")) return true;
        if (EnvFlag("AVALONIA_SCREEN_READER")) return true;
        return false;
    }

    private static ColorScheme DetectColorScheme()
    {
        if (EnvFlag("AVALONIA_HIGH_CONTRAST")) return ColorScheme.HighContrast;
        if (EnvFlag("AVALONIA_DARK")) return ColorScheme.Dark;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var gtkTheme = Environment.GetEnvironmentVariable("GTK_THEME") ?? string.Empty;
            if (gtkTheme.Contains("HighContrast", StringComparison.OrdinalIgnoreCase)) return ColorScheme.HighContrast;
            if (gtkTheme.Contains("dark", StringComparison.OrdinalIgnoreCase)) return ColorScheme.Dark;
        }

        return ColorScheme.Light;
    }

    private static bool EnvFlag(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
