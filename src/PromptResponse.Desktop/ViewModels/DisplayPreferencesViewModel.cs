using System.ComponentModel;
using System.Runtime.CompilerServices;
using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// ViewModel backing the Display Preferences panel. Exposes every capability flag
/// individually plus a preset picker that composes named groups (Excellent vision,
/// Blind/screen reader, Low vision/HC, Cognitive, Motor). All toggles flow through
/// the <see cref="IProfileService"/> so the application's effective rendering
/// profile updates immediately.
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

    public IRenderingProfile ActiveProfile => _profileService.ActiveProfile;

    // ── Color scheme (mutually-exclusive triplet) ──
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

    public bool IsLight { get => ColorScheme == ColorScheme.Light;        set { if (value) ColorScheme = ColorScheme.Light; } }
    public bool IsDark  { get => ColorScheme == ColorScheme.Dark;         set { if (value) ColorScheme = ColorScheme.Dark; } }
    public bool IsHighContrast { get => ColorScheme == ColorScheme.HighContrast; set { if (value) ColorScheme = ColorScheme.HighContrast; } }

    // ── Global capability flags ──
    public bool LargeText           { get => IsActive<LargeTextProfile>();           set => Toggle<LargeTextProfile>(value); }
    public bool ReducedMotion       { get => IsActive<ReducedMotionProfile>();       set => Toggle<ReducedMotionProfile>(value); }
    public bool ScreenReaderTuned   { get => IsActive<ScreenReaderTunedProfile>();   set => Toggle<ScreenReaderTunedProfile>(value); }
    public bool LargeHitTargets     { get => IsActive<LargeHitTargetsProfile>();     set => Toggle<LargeHitTargetsProfile>(value); }

    // ── Display rendering flags ──
    public bool NumberThousandsSeparators { get => IsActive<NumberThousandsSeparatorsProfile>(); set => Toggle<NumberThousandsSeparatorsProfile>(value); }
    public bool CurrencyDisplay           { get => IsActive<CurrencyDisplayProfile>();           set => Toggle<CurrencyDisplayProfile>(value); }
    public bool IsoDatePrettify           { get => IsActive<IsoDatePrettifyProfile>();           set => Toggle<IsoDatePrettifyProfile>(value); }
    public bool DisplaysAsPreview         { get => IsActive<DisplaysAsPreviewProfile>();         set => Toggle<DisplaysAsPreviewProfile>(value); }

    // ── Interactive widget flags ──
    public bool CalendarPicker { get => IsActive<CalendarPickerProfile>(); set => Toggle<CalendarPickerProfile>(value); }
    public bool BooleanRadios  { get => IsActive<BooleanRadiosProfile>();  set => Toggle<BooleanRadiosProfile>(value); }

    // ── Input mask flags ──
    public bool PhoneInputMask      { get => IsActive<PhoneInputMaskProfile>();      set => Toggle<PhoneInputMaskProfile>(value); }
    public bool SsnInputMask        { get => IsActive<SsnInputMaskProfile>();        set => Toggle<SsnInputMaskProfile>(value); }
    public bool EinInputMask        { get => IsActive<EinInputMaskProfile>();        set => Toggle<EinInputMaskProfile>(value); }
    public bool ZipInputMask        { get => IsActive<ZipInputMaskProfile>();        set => Toggle<ZipInputMaskProfile>(value); }
    public bool CurrencyInputMask   { get => IsActive<CurrencyInputMaskProfile>();   set => Toggle<CurrencyInputMaskProfile>(value); }
    public bool PercentageInputMask { get => IsActive<PercentageInputMaskProfile>(); set => Toggle<PercentageInputMaskProfile>(value); }

    /// <summary>Applies a named preset by composing its flag set on top of the current
    /// color scheme. See <see cref="ProfilePresets"/> for the composition rules.</summary>
    public void ApplyPreset(ProfilePresets.Preset preset) => ProfilePresets.Apply(preset, _profileService);

    /// <summary>Restores OS-detected defaults; clears all user-toggled enhancements.</summary>
    public void Reset() => _profileService.Reset();

    private bool IsActive<TProfile>() where TProfile : IRenderingProfile => _profileService.IsActive(typeof(TProfile));

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
        Notify(nameof(LargeText));
        Notify(nameof(ReducedMotion));
        Notify(nameof(ScreenReaderTuned));
        Notify(nameof(LargeHitTargets));
        Notify(nameof(NumberThousandsSeparators));
        Notify(nameof(CurrencyDisplay));
        Notify(nameof(IsoDatePrettify));
        Notify(nameof(DisplaysAsPreview));
        Notify(nameof(CalendarPicker));
        Notify(nameof(BooleanRadios));
        Notify(nameof(PhoneInputMask));
        Notify(nameof(SsnInputMask));
        Notify(nameof(EinInputMask));
        Notify(nameof(ZipInputMask));
        Notify(nameof(CurrencyInputMask));
        Notify(nameof(PercentageInputMask));
    }

    private void Notify([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
