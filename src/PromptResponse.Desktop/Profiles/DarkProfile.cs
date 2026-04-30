using Avalonia;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Dark color scheme. Capability profile (not just preference) — some users need
/// low-light displays due to photophobia, migraine triggers, or low-light environments.
/// </summary>
public sealed class DarkProfile : IRenderingProfile
{
    public string Name => "Dark";
    public string? FormatDisplay(string? rawValue, string? typeHint) => rawValue;
    public Size MinimumTouchTarget => new(36, 36);
    public bool AnimationsEnabled => true;
    public LiveRegionVerbosity LiveRegions => LiveRegionVerbosity.Normal;
    public ContrastLevel TargetContrast => ContrastLevel.AA;
    public double TextScale => 1.0;
    public bool ColorCuesEnabled => true;
    public ColorScheme ColorScheme => ColorScheme.Dark;
}
