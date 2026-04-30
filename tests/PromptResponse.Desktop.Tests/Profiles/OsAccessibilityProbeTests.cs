using FluentAssertions;
using PromptResponse.Desktop.Profiles;
using Xunit;

namespace PromptResponse.Desktop.Tests.Profiles;

/// <summary>
/// Tests OS preference detection through the env-var override channel that's portable
/// across CI hosts. Native platform-specific detection is exercised by integration
/// tests on actual hosts.
/// </summary>
[Collection("OsAccessibilityProbe")]
public class OsAccessibilityProbeTests : IDisposable
{
    private readonly Dictionary<string, string?> _saved = new();

    private static readonly string[] EnvKeys =
    {
        "AVALONIA_HIGH_CONTRAST",
        "AVALONIA_REDUCED_MOTION",
        "AVALONIA_SCREEN_READER",
        "AVALONIA_DARK",
        "GTK_THEME",
        "GNOME_REDUCED_MOTION",
        "ORCA_RUNNING",
    };

    public OsAccessibilityProbeTests()
    {
        // Snapshot and clear env to isolate each test.
        foreach (var key in EnvKeys)
        {
            _saved[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    public void Dispose()
    {
        foreach (var (key, value) in _saved)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    [Fact]
    public void NoEnvSignal_ProbeReturnsConservativeDefaults()
    {
        var probe = new OsAccessibilityProbe();

        probe.HighContrast.Should().BeFalse();
        probe.ReducedMotion.Should().BeFalse();
        probe.ScreenReaderActive.Should().BeFalse();
        probe.PreferredColorScheme.Should().Be(ColorScheme.Light);
    }

    [Fact]
    public void EnvOverride_HighContrast_BothFlagsAndSchemeReportTrue()
    {
        Environment.SetEnvironmentVariable("AVALONIA_HIGH_CONTRAST", "1");

        var probe = new OsAccessibilityProbe();

        probe.HighContrast.Should().BeTrue();
        probe.PreferredColorScheme.Should().Be(ColorScheme.HighContrast);
    }

    [Fact]
    public void EnvOverride_ReducedMotion_FlagReportsTrue()
    {
        Environment.SetEnvironmentVariable("AVALONIA_REDUCED_MOTION", "true");

        var probe = new OsAccessibilityProbe();

        probe.ReducedMotion.Should().BeTrue();
    }

    [Fact]
    public void EnvOverride_DarkMode_SchemeIsDark()
    {
        Environment.SetEnvironmentVariable("AVALONIA_DARK", "1");

        var probe = new OsAccessibilityProbe();

        probe.PreferredColorScheme.Should().Be(ColorScheme.Dark);
    }

    [Fact]
    public void EnvOverride_OrcaRunning_ScreenReaderIsActive()
    {
        Environment.SetEnvironmentVariable("ORCA_RUNNING", "1");

        var probe = new OsAccessibilityProbe();

        probe.ScreenReaderActive.Should().BeTrue();
    }

    [Fact]
    public void EnvOverride_AvaloniaScreenReader_ScreenReaderIsActive()
    {
        Environment.SetEnvironmentVariable("AVALONIA_SCREEN_READER", "true");

        var probe = new OsAccessibilityProbe();

        probe.ScreenReaderActive.Should().BeTrue();
    }

    [Fact]
    public void GtkTheme_HighContrast_TriggersHighContrastDetection()
    {
        if (!OperatingSystem.IsLinux())
        {
            return; // GTK_THEME logic is Linux-only.
        }
        Environment.SetEnvironmentVariable("GTK_THEME", "HighContrast");

        var probe = new OsAccessibilityProbe();

        probe.HighContrast.Should().BeTrue();
        probe.PreferredColorScheme.Should().Be(ColorScheme.HighContrast);
    }

    [Fact]
    public void GtkTheme_DarkVariant_TriggersDarkScheme()
    {
        if (!OperatingSystem.IsLinux()) return;
        Environment.SetEnvironmentVariable("GTK_THEME", "Adwaita-dark");

        var probe = new OsAccessibilityProbe();

        probe.PreferredColorScheme.Should().Be(ColorScheme.Dark);
    }

    [Fact]
    public void GnomeReducedMotion_FlagReportsTrue()
    {
        if (!OperatingSystem.IsLinux()) return;
        Environment.SetEnvironmentVariable("GNOME_REDUCED_MOTION", "1");

        var probe = new OsAccessibilityProbe();

        probe.ReducedMotion.Should().BeTrue();
    }
}
