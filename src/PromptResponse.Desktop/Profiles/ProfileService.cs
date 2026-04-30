namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Default <see cref="IProfileService"/> implementation. Auto-detects OS accessibility
/// preferences at construction, then merges user-selected enhancements on top.
/// </summary>
public sealed class ProfileService : IProfileService
{
    private readonly IOsAccessibilityProbe _osProbe;
    private readonly Dictionary<Type, IRenderingProfile> _active = new();
    private IRenderingProfile _composite = new DefaultProfile();

    public ProfileService(IOsAccessibilityProbe osProbe)
    {
        _osProbe = osProbe ?? throw new ArgumentNullException(nameof(osProbe));
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
    }

    private void Recompose()
    {
        _composite = CompositeProfile.Of(_active.Values.ToArray());
    }
}
