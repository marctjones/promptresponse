using System;
using System.Runtime.InteropServices;

namespace PromptResponse.AccessibilityTests;

/// <summary>
/// Factory for creating platform-specific accessibility inspectors.
/// </summary>
public static class AccessibilityInspectorFactory
{
    /// <summary>
    /// Creates an accessibility inspector for the current platform.
    /// </summary>
    /// <returns>Platform-specific inspector.</returns>
    public static IAccessibilityInspector CreateInspector()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxAccessibilityInspector();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsAccessibilityInspector();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new MacAccessibilityInspector();
        }

        throw new PlatformNotSupportedException(
            $"Accessibility testing not supported on platform: {RuntimeInformation.OSDescription}");
    }

    /// <summary>
    /// Checks if accessibility testing is available on the current platform.
    /// </summary>
    public static async Task<bool> IsAccessibilityTestingAvailableAsync()
    {
        try
        {
            var inspector = CreateInspector();
            return await inspector.IsAvailableAsync();
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
    }
}
