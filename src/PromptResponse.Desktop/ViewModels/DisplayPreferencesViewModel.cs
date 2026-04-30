using System.ComponentModel;
using System.Runtime.CompilerServices;
using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// ViewModel backing the Display Preferences panel. Exposes the seven enhancement
/// profiles as toggleable booleans plus a color-scheme choice. All changes flow
/// through the IProfileService so the application's effective rendering profile
/// updates immediately.
/// </summary>
public sealed class DisplayPreferencesViewModel : INotifyPropertyChanged
{
    private readonly IProfileService _profileService;

    public DisplayPreferencesViewModel(IProfileService profileService)
    {
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _profileService.ProfileChanged += OnProfileChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Convenience: the active composite profile, for views that bind to its properties.</summary>
    public IRenderingProfile ActiveProfile => _profileService.ActiveProfile;

    public ColorScheme ColorScheme
    {
        get => _profileService.ActiveProfile.ColorScheme;
        set
        {
            if (_profileService.ActiveProfile.ColorScheme != value)
            {
                _profileService.SetColorScheme(value);
                Notify(nameof(ColorScheme));
            }
        }
    }

    public bool IsLight
    {
        get => ColorScheme == ColorScheme.Light;
        set { if (value) ColorScheme = ColorScheme.Light; }
    }

    public bool IsDark
    {
        get => ColorScheme == ColorScheme.Dark;
        set { if (value) ColorScheme = ColorScheme.Dark; }
    }

    public bool IsHighContrast
    {
        get => ColorScheme == ColorScheme.HighContrast;
        set { if (value) ColorScheme = ColorScheme.HighContrast; }
    }

    public bool VisualFormatting
    {
        get => _profileService.IsActive(typeof(VisualFormattingProfile));
        set => Toggle<VisualFormattingProfile>(value);
    }

    public bool LargeText
    {
        get => _profileService.IsActive(typeof(LargeTextProfile));
        set => Toggle<LargeTextProfile>(value);
    }

    public bool ReducedMotion
    {
        get => _profileService.IsActive(typeof(ReducedMotionProfile));
        set => Toggle<ReducedMotionProfile>(value);
    }

    public bool ScreenReaderTuned
    {
        get => _profileService.IsActive(typeof(ScreenReaderTunedProfile));
        set => Toggle<ScreenReaderTunedProfile>(value);
    }

    public bool MotorAssist
    {
        get => _profileService.IsActive(typeof(MotorAssistProfile));
        set => Toggle<MotorAssistProfile>(value);
    }

    /// <summary>Restores OS-detected defaults; clears all user-toggled enhancements.</summary>
    public void Reset() => _profileService.Reset();

    private void Toggle<TProfile>(bool enable) where TProfile : IRenderingProfile, new()
    {
        if (enable && !_profileService.IsActive(typeof(TProfile)))
        {
            _profileService.Enable<TProfile>();
        }
        else if (!enable && _profileService.IsActive(typeof(TProfile)))
        {
            _profileService.Disable<TProfile>();
        }
    }

    private void OnProfileChanged(object? sender, EventArgs e)
    {
        // Any profile change can affect any of our derived properties; pulse them all.
        Notify(nameof(ActiveProfile));
        Notify(nameof(ColorScheme));
        Notify(nameof(IsLight));
        Notify(nameof(IsDark));
        Notify(nameof(IsHighContrast));
        Notify(nameof(VisualFormatting));
        Notify(nameof(LargeText));
        Notify(nameof(ReducedMotion));
        Notify(nameof(ScreenReaderTuned));
        Notify(nameof(MotorAssist));
    }

    private void Notify([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
