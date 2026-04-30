namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Reads accessibility-related preferences from the host OS so the application can
/// auto-engage matching capability profiles at startup.
/// </summary>
/// <remarks>
/// On Windows reads SystemParameters.HighContrast / EnableEffects / Narrator state.
/// On macOS reads NSWorkspace accessibility preferences.
/// On Linux reads GTK / GNOME settings.
/// Mocked in tests via a stub implementation.
/// </remarks>
public interface IOsAccessibilityProbe
{
    /// <summary>True when the OS reports a high-contrast or "increase contrast" preference.</summary>
    bool HighContrast { get; }

    /// <summary>True when the OS reports prefers-reduced-motion.</summary>
    bool ReducedMotion { get; }

    /// <summary>True when a screen reader (Narrator, Orca, VoiceOver) is detected as running.</summary>
    bool ScreenReaderActive { get; }

    /// <summary>OS-preferred color scheme (Light, Dark, or HighContrast).</summary>
    ColorScheme PreferredColorScheme { get; }
}
