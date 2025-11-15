using Avalonia.Media;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Provides platform-specific feature detection for cross-platform UI adaptation.
/// Allows the app to gracefully adapt to platform capabilities while maintaining
/// consistent functionality across Windows, Linux, and macOS.
/// </summary>
public interface IPlatformFeatures
{
    /// <summary>
    /// Gets whether the platform supports acrylic/blur transparency effects.
    /// Windows: true, Linux: false (uses solid colors), macOS: varies
    /// </summary>
    bool SupportsAcrylic { get; }

    /// <summary>
    /// Gets whether custom title bar customization is recommended for this platform.
    /// Windows: true, Linux: false (respect native), macOS: varies
    /// </summary>
    bool SupportsCustomTitleBar { get; }

    /// <summary>
    /// Gets whether the platform provides a system-wide accent color.
    /// Windows: true, Linux: false, macOS: true
    /// </summary>
    bool SupportsSystemAccentColor { get; }

    /// <summary>
    /// Gets the system accent color or a default if not available.
    /// </summary>
    /// <returns>The accent color to use for primary UI elements</returns>
    Color GetAccentColor();

    /// <summary>
    /// Gets whether the user prefers reduced motion (accessibility setting).
    /// Used to disable or reduce animations for users with vestibular disorders.
    /// </summary>
    /// <returns>True if animations should be minimal or disabled</returns>
    bool PrefersReducedMotion();

    /// <summary>
    /// Gets the recommended animation duration based on platform and user preferences.
    /// Returns 0 if reduced motion is preferred, otherwise returns the specified duration.
    /// </summary>
    /// <param name="normalDuration">Duration in milliseconds when animations are enabled</param>
    /// <returns>Actual duration to use (0 if reduced motion preferred)</returns>
    double GetAnimationDuration(double normalDuration);
}
