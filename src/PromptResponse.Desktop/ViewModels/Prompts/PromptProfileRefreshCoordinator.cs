using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Owns the profile-service subscription and the complete binding refresh that a
/// prompt needs after a rendering-profile transition.
/// </summary>
/// <remarks>
/// Keeping this lifecycle separate from prompt mutation makes the accessibility
/// contract auditable: display formatting, live-region wording, colour cues and
/// raw-editor geometry always refresh as one profile transition.
/// </remarks>
internal sealed class PromptProfileRefreshCoordinator : IDisposable
{
    private readonly IProfileService _profileService;
    private readonly Action<string> _notify;
    private readonly Action _refreshDerivedProperties;
    private bool _disposed;

    internal PromptProfileRefreshCoordinator(
        IProfileService profileService,
        Action<string> notify,
        Action refreshDerivedProperties)
    {
        _profileService = profileService;
        _notify = notify;
        _refreshDerivedProperties = refreshDerivedProperties;
        _profileService.ProfileChanged += OnProfileChanged;
    }

    private void OnProfileChanged(object? sender, EventArgs e)
    {
        _notify(nameof(PromptViewModelBase.DisplayValue));
        _notify(nameof(PromptViewModelBase.ProvenanceAnnouncement));
        _notify(nameof(PromptViewModelBase.ProvenanceColorCue));
        _notify(nameof(PromptViewModelBase.ToggleGlyphSize));
        _notify(nameof(PromptViewModelBase.ToggleButtonSize));
        _refreshDerivedProperties();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _profileService.ProfileChanged -= OnProfileChanged;
    }
}
