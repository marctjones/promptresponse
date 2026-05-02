namespace PromptResponse.Desktop.Models;

/// <summary>
/// Application settings that persist across sessions.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Window settings.
    /// </summary>
    public WindowSettings Window { get; set; } = new();

    /// <summary>
    /// Capability-profile flags persisted across launches. <c>null</c> indicates a
    /// fresh-install state where ProfileService should apply OS-detected defaults
    /// + the "Excellent vision" preset (the latter only when no special
    /// accommodations were detected). Once the user toggles anything, this becomes
    /// non-null and from then on the saved choice wins on every launch.
    /// </summary>
    public ProfileSettings? Profile { get; set; }
}

/// <summary>
/// Persisted capability-profile state. Stores the active flag set as a list of
/// type short-names (e.g. <c>"PhoneInputMaskProfile"</c>) so the JSON survives
/// minor refactors better than reflective full type names. ProfileService maps
/// names back to types via the registered profile classes.
/// </summary>
public class ProfileSettings
{
    /// <summary>Active color scheme — "Light", "Dark", or "HighContrast".</summary>
    public string ColorScheme { get; set; } = "Light";

    /// <summary>Short type names (without namespace) of every active enhancement
    /// profile. Mapped back to types in ProfileService.</summary>
    public List<string> ActiveFlags { get; set; } = new();
}

/// <summary>
/// Window size and position settings.
/// </summary>
public class WindowSettings
{
    /// <summary>
    /// Window width in pixels.
    /// </summary>
    public double Width { get; set; } = 900;

    /// <summary>
    /// Window height in pixels.
    /// </summary>
    public double Height { get; set; } = 700;

    /// <summary>
    /// Window X position.
    /// </summary>
    public double? X { get; set; }

    /// <summary>
    /// Window Y position.
    /// </summary>
    public double? Y { get; set; }

    /// <summary>
    /// Whether the window is maximized.
    /// </summary>
    public bool IsMaximized { get; set; }
}
