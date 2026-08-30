using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.ViewModels.Workflows;

/// <summary>
/// Owns the profile-driven wizard navigation state used by the desktop shell.
/// Keeping the index, its bounds, and preset application together prevents the
/// shell from having two competing interpretations of the active profile.
/// </summary>
internal sealed class WizardProfileWorkflow : IDisposable
{
    private readonly IProfileService _profiles;
    private readonly Func<int> _sectionCount;
    private readonly Func<int, string?> _sectionTitle;
    private int _sectionIndex;

    public WizardProfileWorkflow(
        IProfileService profiles,
        Func<int> sectionCount,
        Func<int, string?> sectionTitle)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _sectionCount = sectionCount ?? throw new ArgumentNullException(nameof(sectionCount));
        _sectionTitle = sectionTitle ?? throw new ArgumentNullException(nameof(sectionTitle));
        _profiles.ProfileChanged += OnProfileChanged;
    }

    public event Action<WizardProfileChange>? StateChanged;

    public int SectionIndex => _sectionIndex;

    public bool IsWizardMode => _profiles.IsActive(typeof(WizardModeProfile));

    public bool HasCurrentSection => _sectionIndex >= 0 && _sectionIndex < _sectionCount();

    public string StepLabel
    {
        get
        {
            var count = _sectionCount();
            if (count == 0) return string.Empty;

            var index = Math.Clamp(_sectionIndex, 0, count - 1);
            var title = _sectionTitle(index);
            return $"Section {index + 1} of {count}: " +
                   (string.IsNullOrWhiteSpace(title) ? "(untitled)" : title);
        }
    }

    public bool CanPrevious => _sectionIndex > 0;

    public bool CanNext => _sectionIndex < _sectionCount() - 1;

    public void SetSectionIndex(int index)
    {
        var clamped = Math.Clamp(index, 0, Math.Max(0, _sectionCount() - 1));
        if (clamped == _sectionIndex) return;
        _sectionIndex = clamped;
        StateChanged?.Invoke(WizardProfileChange.Navigation);
    }

    public void MovePrevious() => SetSectionIndex(_sectionIndex - 1);

    public void MoveNext() => SetSectionIndex(_sectionIndex + 1);

    /// <summary>Resets a newly opened document to its first section. The event
    /// is intentionally raised even when already at zero because section titles
    /// and the available bounds have changed.</summary>
    public void ResetForDocument() => SetSectionIndexAndNotify(0);

    /// <summary>Re-evaluates navigation after the shell changes its section tree.</summary>
    public void RefreshSections() => SetSectionIndexAndNotify(
        Math.Min(_sectionIndex, Math.Max(0, _sectionCount() - 1)));

    public void ToggleWizardMode()
    {
        if (IsWizardMode)
            _profiles.Disable<WizardModeProfile>();
        else
            _profiles.Enable<WizardModeProfile>();
    }

    public void ApplyPreset(string? presetName)
    {
        if (string.IsNullOrWhiteSpace(presetName)) return;
        if (Enum.TryParse<ProfilePresets.Preset>(presetName, out var preset))
            ProfilePresets.Apply(preset, _profiles);
    }

    public void Dispose() => _profiles.ProfileChanged -= OnProfileChanged;

    private void SetSectionIndexAndNotify(int index)
    {
        _sectionIndex = index;
        StateChanged?.Invoke(WizardProfileChange.Navigation);
    }

    private void OnProfileChanged(object? sender, EventArgs e) =>
        StateChanged?.Invoke(WizardProfileChange.ProfilePresentation);
}

[Flags]
internal enum WizardProfileChange
{
    Navigation = 1,
    ProfilePresentation = 2,
}
