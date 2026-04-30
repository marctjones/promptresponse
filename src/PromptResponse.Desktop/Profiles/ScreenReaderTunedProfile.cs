using Avalonia;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Optimises announcements for users whose capability profile relies on screen
/// readers: verbose live regions, suppressed cosmetic text that distracts speech
/// output, animations disabled (animations disrupt screen-reader flow).
/// </summary>
public sealed class ScreenReaderTunedProfile : IRenderingProfile
{
    public string Name => "ScreenReaderTuned";
    public string? FormatDisplay(string? rawValue, string? typeHint) => rawValue;
    public Size MinimumTouchTarget => new(36, 36);
    public bool AnimationsEnabled => false;
    public LiveRegionVerbosity LiveRegions => LiveRegionVerbosity.Verbose;
    public ContrastLevel TargetContrast => ContrastLevel.AA;
    public double TextScale => 1.0;
    public bool ColorCuesEnabled => true;
    public ColorScheme ColorScheme => ColorScheme.Light;
}
