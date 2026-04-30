namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Tracks the user's active capability-profile composition and exposes it as a
/// merged <see cref="IRenderingProfile"/>. Auto-detects OS preferences at startup
/// and emits <see cref="ProfileChanged"/> on every transition so the UI can
/// re-render.
/// </summary>
public interface IProfileService
{
    /// <summary>The merged composite of all currently-active profiles.</summary>
    IRenderingProfile ActiveProfile { get; }

    /// <summary>True if a profile of the given type is currently active.</summary>
    bool IsActive(Type profileType);

    /// <summary>Activates the named profile (idempotent).</summary>
    void Enable<TProfile>() where TProfile : IRenderingProfile, new();

    /// <summary>Deactivates the named profile (idempotent).</summary>
    void Disable<TProfile>() where TProfile : IRenderingProfile;

    /// <summary>Replaces the current color-scheme profile (Light / Dark / HighContrast).</summary>
    void SetColorScheme(ColorScheme scheme);

    /// <summary>Clears user-selected enhancements and restores OS-detected defaults.</summary>
    void Reset();

    /// <summary>Raised after any change to the active set.</summary>
    event EventHandler? ProfileChanged;
}
