using System;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Microsoft.Extensions.Logging;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Cross-platform feature detection service.
/// Provides platform-specific capabilities and graceful fallbacks.
/// </summary>
public class PlatformFeatures : IPlatformFeatures
{
    private readonly ILogger<PlatformFeatures> _logger;
    private readonly bool _isWindows;
    private readonly bool _isLinux;
    private readonly bool _isMacOS;

    public PlatformFeatures(ILogger<PlatformFeatures> logger)
    {
        _logger = logger;
        _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        _isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        _isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        _logger.LogInformation("Platform detected: Windows={IsWindows}, Linux={IsLinux}, macOS={IsMacOS}",
            _isWindows, _isLinux, _isMacOS);
    }

    public bool SupportsAcrylic => _isWindows;

    public bool SupportsCustomTitleBar => _isWindows;

    public bool SupportsSystemAccentColor => _isWindows || _isMacOS;

    public Color GetAccentColor()
    {
        if (_isWindows)
        {
            // TODO: In a future enhancement, we could read Windows Registry for accent color
            // For now, return a sensible default blue that matches Windows
            return Color.Parse("#0078D4");
        }
        else if (_isMacOS)
        {
            // macOS has system accent color, but requires platform-specific code
            // For now, return default blue
            return Color.Parse("#007AFF");
        }
        else // Linux
        {
            // Linux doesn't have universal accent color concept
            // Return a pleasant blue that works with most GTK themes
            return Color.Parse("#0078D4");
        }
    }

    public bool PrefersReducedMotion()
    {
        // TODO: Platform-specific accessibility checks
        // Windows: Check SystemParameters
        // Linux: Check GTK settings
        // macOS: Check NSWorkspace

        // For now, return false (animations enabled by default)
        // This is safe because we keep animations subtle and short
        return false;
    }

    public double GetAnimationDuration(double normalDuration)
    {
        return PrefersReducedMotion() ? 0 : normalDuration;
    }
}
