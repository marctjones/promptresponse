using Avalonia;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Color-scheme dimension a profile selects. Light/Dark/HighContrast are capability
/// choices (not preferences) per the project vision: photophobia, migraine, low vision,
/// and similar capabilities make this a first-class accessibility concern.
/// </summary>
public enum ColorScheme
{
    Light,
    Dark,
    HighContrast,
}

/// <summary>
/// Contrast budget the profile aims for. AA is the project floor; AAA is the
/// HighContrast target.
/// </summary>
public enum ContrastLevel
{
    AA,
    AAA,
}

/// <summary>
/// Verbosity of screen-reader live-region announcements.
/// </summary>
public enum LiveRegionVerbosity
{
    /// <summary>Suppresses all but truly important announcements (errors, document state changes).</summary>
    Quiet,
    /// <summary>Default behaviour: state changes and validation summaries are announced.</summary>
    Normal,
    /// <summary>Announces extra context (current section, focused prompt, mode hints) for screen-reader users.</summary>
    Verbose,
}

/// <summary>
/// Capability-profile contract. Each rule is an additive enhancement layered atop the
/// universal core.
/// </summary>
/// <remarks>
/// IMPORTANT data-integrity invariant: <see cref="FormatDisplay"/> may render a value
/// for display, but the stored response is always the raw user input. Profiles never
/// mutate persisted data — they transform display only. See feedback memory
/// "capability_profile_design".
/// </remarks>
public interface IRenderingProfile
{
    /// <summary>Stable identifier used for persisted settings and telemetry.</summary>
    string Name { get; }

    /// <summary>
    /// Renders a stored raw response for display. Must return the input unchanged when
    /// it doesn't match the suggested type-hint, so that "five" in a number-hinted
    /// prompt remains visible as "five" — never silently dropped or mangled.
    /// </summary>
    string? FormatDisplay(string? rawValue, string? typeHint);

    /// <summary>Minimum width × height of touch / click targets enforced by this profile.</summary>
    Size MinimumTouchTarget { get; }

    /// <summary>Whether decorative animations (transitions, fades) are permitted.</summary>
    bool AnimationsEnabled { get; }

    /// <summary>Verbosity of screen-reader live regions.</summary>
    LiveRegionVerbosity LiveRegions { get; }

    /// <summary>Contrast budget the profile aims for.</summary>
    ContrastLevel TargetContrast { get; }

    /// <summary>Multiplier applied to the typography scale (1.0 = default, 1.5 = LargeText).</summary>
    double TextScale { get; }

    /// <summary>Whether color is used as a redundant cue (in addition to icon + text).</summary>
    bool ColorCuesEnabled { get; }

    /// <summary>Color-scheme this profile selects.</summary>
    ColorScheme ColorScheme { get; }
}
