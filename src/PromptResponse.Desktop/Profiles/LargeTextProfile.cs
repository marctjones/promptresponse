using Avalonia;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Scales typography by 1.5× for users whose capability profile benefits from larger
/// text (low vision, dyslexia, presbyopia). Pairs naturally with <see cref="HighContrastProfile"/>.
/// </summary>
public sealed class LargeTextProfile : IRenderingProfile
{
    public string Name => "LargeText";
    public string? FormatDisplay(string? rawValue, string? typeHint) => rawValue;
    public Size MinimumTouchTarget => new(36, 36);
    public bool AnimationsEnabled => true;
    public LiveRegionVerbosity LiveRegions => LiveRegionVerbosity.Normal;
    public ContrastLevel TargetContrast => ContrastLevel.AA;
    public double TextScale => 1.5;
    public bool ColorCuesEnabled => true;
    public ColorScheme ColorScheme => ColorScheme.Light;
}
