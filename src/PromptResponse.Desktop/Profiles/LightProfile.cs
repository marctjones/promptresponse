using Avalonia;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Light color scheme. Capability profile (not just preference) — some users need
/// light environments due to photophobia, dyslexia, or specific visual conditions.
/// </summary>
public sealed class LightProfile : IRenderingProfile
{
    public string Name => "Light";
    public string? FormatDisplay(string? rawValue, string? typeHint) => rawValue;
    public Size MinimumTouchTarget => new(36, 36);
    public bool AnimationsEnabled => true;
    public LiveRegionVerbosity LiveRegions => LiveRegionVerbosity.Normal;
    public ContrastLevel TargetContrast => ContrastLevel.AA;
    public double TextScale => 1.0;
    public bool ColorCuesEnabled => true;
    public ColorScheme ColorScheme => ColorScheme.Light;
}
