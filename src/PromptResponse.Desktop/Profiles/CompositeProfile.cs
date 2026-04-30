using Avalonia;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Merges multiple <see cref="IRenderingProfile"/>s into a single effective profile
/// using "most accommodating wins" semantics — bigger touch targets, higher contrast,
/// larger text, more verbose live regions, and any "no animations" vote disables
/// animations entirely.
/// </summary>
/// <remarks>
/// Order semantics:
///   - Touch target: max(width, height) across all profiles.
///   - Animations: any profile that disables animations wins (capability hard-stop).
///   - Live region verbosity: max across all profiles (Quiet → Normal → Verbose).
///   - Contrast: max across all (AA → AAA).
///   - Text scale: max across all.
///   - ColorCues: AND across all (any profile that disables it wins to be safe).
///   - ColorScheme: the LAST profile that explicitly carries one wins, so user-chosen
///     ordering is meaningful for theme selection.
///   - FormatDisplay: the first profile whose result differs from the raw input wins;
///     if none transform, returns raw input unchanged.
///   - Name: concatenated, "+" separated, for telemetry and persisted settings.
///
/// Empty composition falls back to <see cref="DefaultProfile"/>.
/// </remarks>
public sealed class CompositeProfile : IRenderingProfile
{
    private static readonly IRenderingProfile DefaultFallback = new DefaultProfile();
    private readonly IReadOnlyList<IRenderingProfile> _profiles;

    private CompositeProfile(IReadOnlyList<IRenderingProfile> profiles)
    {
        _profiles = profiles;
    }

    /// <summary>
    /// Builds a composite from zero or more profiles. Order matters for the
    /// <see cref="ColorScheme"/> and <see cref="FormatDisplay"/> rules.
    /// </summary>
    public static IRenderingProfile Of(params IRenderingProfile[] profiles)
    {
        if (profiles == null || profiles.Length == 0)
        {
            return DefaultFallback;
        }
        return new CompositeProfile(profiles);
    }

    public string Name => string.Join("+", _profiles.Select(p => p.Name));

    public string? FormatDisplay(string? rawValue, string? typeHint)
    {
        if (rawValue == null) return null;
        foreach (var profile in _profiles)
        {
            var formatted = profile.FormatDisplay(rawValue, typeHint);
            if (!string.Equals(formatted, rawValue, StringComparison.Ordinal))
            {
                return formatted;
            }
        }
        return rawValue;
    }

    public Size MinimumTouchTarget
    {
        get
        {
            double width = DefaultFallback.MinimumTouchTarget.Width;
            double height = DefaultFallback.MinimumTouchTarget.Height;
            foreach (var profile in _profiles)
            {
                width = Math.Max(width, profile.MinimumTouchTarget.Width);
                height = Math.Max(height, profile.MinimumTouchTarget.Height);
            }
            return new Size(width, height);
        }
    }

    public bool AnimationsEnabled => _profiles.All(p => p.AnimationsEnabled);

    public LiveRegionVerbosity LiveRegions =>
        _profiles.Aggregate(LiveRegionVerbosity.Quiet, (acc, p) => p.LiveRegions > acc ? p.LiveRegions : acc);

    public ContrastLevel TargetContrast =>
        _profiles.Any(p => p.TargetContrast == ContrastLevel.AAA) ? ContrastLevel.AAA : ContrastLevel.AA;

    public double TextScale =>
        _profiles.Aggregate(1.0, (acc, p) => Math.Max(acc, p.TextScale));

    public bool ColorCuesEnabled => _profiles.All(p => p.ColorCuesEnabled);

    public ColorScheme ColorScheme
    {
        get
        {
            // Last explicit color-scheme profile wins.
            ColorScheme scheme = ColorScheme.Light;
            foreach (var profile in _profiles)
            {
                if (profile is LightProfile or DarkProfile or HighContrastProfile)
                {
                    scheme = profile.ColorScheme;
                }
            }
            // If no color-scheme-bearing profile was active, but a profile of any kind
            // explicitly carries a non-Light scheme, honour that.
            if (!_profiles.Any(p => p is LightProfile or DarkProfile or HighContrastProfile))
            {
                foreach (var profile in _profiles)
                {
                    if (profile.ColorScheme != ColorScheme.Light)
                    {
                        scheme = profile.ColorScheme;
                    }
                }
            }
            return scheme;
        }
    }
}
