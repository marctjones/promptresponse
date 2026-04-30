using Avalonia;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// HighContrast: WCAG AAA contrast (≥7:1 normal text, ≥4.5:1 large), thick borders,
/// no transparency, animations disabled to reduce visual noise. Auto-engaged when the
/// OS reports high-contrast preference (Windows HCM, macOS Increase Contrast,
/// GNOME high-contrast).
/// </summary>
public sealed class HighContrastProfile : IRenderingProfile
{
    public string Name => "HighContrast";
    public string? FormatDisplay(string? rawValue, string? typeHint) => rawValue;
    public Size MinimumTouchTarget => new(44, 44);
    public bool AnimationsEnabled => false;
    public LiveRegionVerbosity LiveRegions => LiveRegionVerbosity.Normal;
    public ContrastLevel TargetContrast => ContrastLevel.AAA;
    public double TextScale => 1.0;
    public bool ColorCuesEnabled => true;
    public ColorScheme ColorScheme => ColorScheme.HighContrast;
}
