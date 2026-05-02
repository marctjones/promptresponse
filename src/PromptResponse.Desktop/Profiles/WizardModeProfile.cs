using Avalonia;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Capability flag that requests wizard-style form rendering: one section at a
/// time with Previous/Next navigation, instead of all sections rendered as one
/// scrolling list. Reduces cognitive load for long forms — paired with the
/// Cognitive/Dyslexia preset by default and surfaceable as its own toggle in
/// View → Wizard Mode.
/// </summary>
/// <remarks>
/// Pure marker flag — no rendering changes from the profile itself; the shell
/// observes <see cref="IProfileService.IsActive"/> for this type and switches
/// its content area between wizard and full-list rendering. All other methods
/// match the default profile.
/// </remarks>
public sealed class WizardModeProfile : IRenderingProfile
{
    public string Name => "WizardMode";
    public string? FormatDisplay(string? rawValue, string? typeHint) => rawValue;
    public Size MinimumTouchTarget => new(36, 36);
    public bool AnimationsEnabled => true;
    public LiveRegionVerbosity LiveRegions => LiveRegionVerbosity.Normal;
    public ContrastLevel TargetContrast => ContrastLevel.AA;
    public double TextScale => 1.0;
    public bool ColorCuesEnabled => true;
    public ColorScheme ColorScheme => ColorScheme.Light;
}
