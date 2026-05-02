using PromptResponse.Desktop.Models;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Default <see cref="IProfileService"/> implementation. Auto-detects OS accessibility
/// preferences at construction, then merges user-selected enhancements on top.
/// </summary>
public sealed class ProfileService : IProfileService
{
    /// <summary>The full set of flag profile types the service knows about. Used
    /// to map persisted short-names back to types when restoring settings.</summary>
    private static readonly Type[] KnownFlagTypes = new[]
    {
        typeof(LightProfile),
        typeof(DarkProfile),
        typeof(HighContrastProfile),
        typeof(LargeTextProfile),
        typeof(ReducedMotionProfile),
        typeof(ScreenReaderTunedProfile),
        typeof(LargeHitTargetsProfile),
        typeof(NumberThousandsSeparatorsProfile),
        typeof(CurrencyDisplayProfile),
        typeof(IsoDatePrettifyProfile),
        typeof(DisplaysAsPreviewProfile),
        typeof(CalendarPickerProfile),
        typeof(BooleanRadiosProfile),
        typeof(PhoneInputMaskProfile),
        typeof(SsnInputMaskProfile),
        typeof(EinInputMaskProfile),
        typeof(ZipInputMaskProfile),
        typeof(CurrencyInputMaskProfile),
        typeof(PercentageInputMaskProfile),
    };

    private readonly IOsAccessibilityProbe _osProbe;
    private readonly bool _applyAffordanceDefaults;
    private readonly Dictionary<Type, IRenderingProfile> _active = new();
    private IRenderingProfile _composite = new DefaultProfile();

    /// <summary>Production constructor — applies the sighted-user affordance
    /// defaults when the OS probe reports no special accommodations.</summary>
    public ProfileService(IOsAccessibilityProbe osProbe)
        : this(osProbe, applyAffordanceDefaults: true) { }

    /// <summary>Test constructor — pass <paramref name="applyAffordanceDefaults"/>
    /// = false for a "no enhancement flags active" baseline. Production callers
    /// always want true (the user expects formatting to work out of the box).</summary>
    public ProfileService(IOsAccessibilityProbe osProbe, bool applyAffordanceDefaults)
    {
        _osProbe = osProbe ?? throw new ArgumentNullException(nameof(osProbe));
        _applyAffordanceDefaults = applyAffordanceDefaults;
        ApplyOsDefaults();
        Recompose();
    }

    public IRenderingProfile ActiveProfile => _composite;

    public event EventHandler? ProfileChanged;

    public bool IsActive(Type profileType)
    {
        if (profileType == null) throw new ArgumentNullException(nameof(profileType));
        return _active.ContainsKey(profileType);
    }

    public void Enable<TProfile>() where TProfile : IRenderingProfile, new()
    {
        var key = typeof(TProfile);
        if (_active.ContainsKey(key)) return;
        _active[key] = new TProfile();
        Recompose();
        ProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Disable<TProfile>() where TProfile : IRenderingProfile
    {
        var key = typeof(TProfile);
        if (!_active.ContainsKey(key)) return;
        _active.Remove(key);
        Recompose();
        ProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetColorScheme(ColorScheme scheme)
    {
        // Replace the active color-scheme-bearing profile (Light/Dark/HighContrast).
        _active.Remove(typeof(LightProfile));
        _active.Remove(typeof(DarkProfile));
        _active.Remove(typeof(HighContrastProfile));
        switch (scheme)
        {
            case ColorScheme.Light:
                _active[typeof(LightProfile)] = new LightProfile();
                break;
            case ColorScheme.Dark:
                _active[typeof(DarkProfile)] = new DarkProfile();
                break;
            case ColorScheme.HighContrast:
                _active[typeof(HighContrastProfile)] = new HighContrastProfile();
                break;
        }
        Recompose();
        ProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        _active.Clear();
        ApplyOsDefaults();
        Recompose();
        ProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    public ProfileSettings Snapshot()
    {
        return new ProfileSettings
        {
            ColorScheme = ActiveProfile.ColorScheme.ToString(),
            ActiveFlags = _active.Keys
                .Where(t => t != typeof(LightProfile) && t != typeof(DarkProfile) && t != typeof(HighContrastProfile))
                .Select(t => t.Name)
                .ToList(),
        };
    }

    public void Restore(ProfileSettings snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

        _active.Clear();

        // Re-apply color scheme first (these profiles are mutually exclusive).
        var scheme = Enum.TryParse<ColorScheme>(snapshot.ColorScheme, out var s) ? s : ColorScheme.Light;
        switch (scheme)
        {
            case ColorScheme.Light: _active[typeof(LightProfile)] = new LightProfile(); break;
            case ColorScheme.Dark: _active[typeof(DarkProfile)] = new DarkProfile(); break;
            case ColorScheme.HighContrast: _active[typeof(HighContrastProfile)] = new HighContrastProfile(); break;
        }

        // Re-apply each saved flag by short type name. Unknown names are ignored
        // (so a future-rename of a flag class doesn't break loading old settings).
        foreach (var name in snapshot.ActiveFlags)
        {
            var type = KnownFlagTypes.FirstOrDefault(t => t.Name == name);
            if (type == null) continue;
            if (_active.ContainsKey(type)) continue;
            _active[type] = (IRenderingProfile)Activator.CreateInstance(type)!;
        }

        Recompose();
        ProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyOsDefaults()
    {
        switch (_osProbe.PreferredColorScheme)
        {
            case ColorScheme.Dark:
                _active[typeof(DarkProfile)] = new DarkProfile();
                break;
            case ColorScheme.HighContrast:
                _active[typeof(HighContrastProfile)] = new HighContrastProfile();
                break;
            default:
                _active[typeof(LightProfile)] = new LightProfile();
                break;
        }

        if (_osProbe.HighContrast && !_active.ContainsKey(typeof(HighContrastProfile)))
        {
            // OS reports high-contrast independent of color scheme — engage anyway.
            _active.Remove(typeof(LightProfile));
            _active.Remove(typeof(DarkProfile));
            _active[typeof(HighContrastProfile)] = new HighContrastProfile();
        }

        if (_osProbe.ReducedMotion)
        {
            _active[typeof(ReducedMotionProfile)] = new ReducedMotionProfile();
        }

        if (_osProbe.ScreenReaderActive)
        {
            _active[typeof(ScreenReaderTunedProfile)] = new ScreenReaderTunedProfile();
        }

        // Default-on visual affordances when no special accommodations are detected.
        // Vision: there is no "normal user" baseline — sighted users get an
        // enhancement profile too. When the OS reports no high-contrast, no screen
        // reader, no reduced-motion, the user is most likely sighted-with-no-other-
        // capabilities, and they expect the formatting affordances browsers and word
        // processors give them out of the box. This matches "ExcellentVision" preset.
        var sightedDefault = _applyAffordanceDefaults
                          && !_osProbe.HighContrast
                          && !_osProbe.ScreenReaderActive
                          && !_osProbe.ReducedMotion;
        if (sightedDefault)
        {
            _active[typeof(NumberThousandsSeparatorsProfile)] = new NumberThousandsSeparatorsProfile();
            _active[typeof(CurrencyDisplayProfile)] = new CurrencyDisplayProfile();
            _active[typeof(IsoDatePrettifyProfile)] = new IsoDatePrettifyProfile();
            _active[typeof(DisplaysAsPreviewProfile)] = new DisplaysAsPreviewProfile();
            _active[typeof(CalendarPickerProfile)] = new CalendarPickerProfile();
            _active[typeof(BooleanRadiosProfile)] = new BooleanRadiosProfile();
            _active[typeof(PhoneInputMaskProfile)] = new PhoneInputMaskProfile();
            _active[typeof(SsnInputMaskProfile)] = new SsnInputMaskProfile();
            _active[typeof(EinInputMaskProfile)] = new EinInputMaskProfile();
            _active[typeof(ZipInputMaskProfile)] = new ZipInputMaskProfile();
            _active[typeof(CurrencyInputMaskProfile)] = new CurrencyInputMaskProfile();
            _active[typeof(PercentageInputMaskProfile)] = new PercentageInputMaskProfile();
        }
    }

    private void Recompose()
    {
        _composite = CompositeProfile.Of(_active.Values.ToArray());
    }
}
