using Avalonia;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Enlarges touch / click targets and disables rapid auto-actions for users whose
/// capability profile includes tremor, limited dexterity, or alternative input
/// devices (head pointers, eye gaze, switch access).
/// </summary>
public sealed class MotorAssistProfile : IRenderingProfile
{
    public string Name => "MotorAssist";
    public string? FormatDisplay(string? rawValue, string? typeHint) => rawValue;
    public Size MinimumTouchTarget => new(56, 56);
    public bool AnimationsEnabled => false;
    public LiveRegionVerbosity LiveRegions => LiveRegionVerbosity.Normal;
    public ContrastLevel TargetContrast => ContrastLevel.AA;
    public double TextScale => 1.0;
    public bool ColorCuesEnabled => true;
    public ColorScheme ColorScheme => ColorScheme.Light;
}
