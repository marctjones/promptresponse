using Avalonia;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Sensible-default base for flag-style rendering profiles. Concrete flag profiles
/// override only the property they actually opine on; all others fall through to
/// the universal-core defaults so <see cref="CompositeProfile"/>'s "most accommodating
/// wins" math doesn't pull a flag's no-opinion default over another flag's intentional
/// override.
/// </summary>
public abstract class RenderingProfileBase : IRenderingProfile
{
    public abstract string Name { get; }

    /// <summary>Default: passthrough. Display-rendering flags (e.g. NumberThousandsSeparators)
    /// override this to reshape the raw value when its type-hint matches.</summary>
    public virtual string? FormatDisplay(string? rawValue, string? typeHint) => rawValue;

    public virtual Size MinimumTouchTarget => new(48, 48);
    public virtual bool AnimationsEnabled => true;
    public virtual LiveRegionVerbosity LiveRegions => LiveRegionVerbosity.Normal;
    public virtual ContrastLevel TargetContrast => ContrastLevel.AA;
    public virtual double TextScale => 1.0;
    public virtual bool ColorCuesEnabled => true;
    public virtual ColorScheme ColorScheme => ColorScheme.Light;
}
