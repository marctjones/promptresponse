using Avalonia;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Disables decorative animations for users whose capability profile benefits from
/// reduced motion (vestibular disorders, motion-sensitivity migraines, ADHD focus).
/// Auto-engaged when the OS reports prefers-reduced-motion.
/// </summary>
public sealed class ReducedMotionProfile : IRenderingProfile
{
    public string Name => "ReducedMotion";
    public string? FormatDisplay(string? rawValue, string? typeHint) => rawValue;
    public Size MinimumTouchTarget => new(36, 36);
    public bool AnimationsEnabled => false;
    public LiveRegionVerbosity LiveRegions => LiveRegionVerbosity.Normal;
    public ContrastLevel TargetContrast => ContrastLevel.AA;
    public double TextScale => 1.0;
    public bool ColorCuesEnabled => true;
    public ColorScheme ColorScheme => ColorScheme.Light;
}
