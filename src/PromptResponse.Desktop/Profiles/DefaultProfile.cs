using Avalonia;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// The universal-core profile: no enhancements, no formatting, raw values, semantic
/// structure only. Everyone starts here regardless of capability profile; other
/// profiles layer on top of this baseline.
/// </summary>
public sealed class DefaultProfile : IRenderingProfile
{
    public string Name => "Default";

    public string? FormatDisplay(string? rawValue, string? typeHint) => rawValue;

    public Size MinimumTouchTarget => new(36, 36);
    public bool AnimationsEnabled => true;
    public LiveRegionVerbosity LiveRegions => LiveRegionVerbosity.Normal;
    public ContrastLevel TargetContrast => ContrastLevel.AA;
    public double TextScale => 1.0;
    public bool ColorCuesEnabled => true;
    public ColorScheme ColorScheme => ColorScheme.Light;
}
