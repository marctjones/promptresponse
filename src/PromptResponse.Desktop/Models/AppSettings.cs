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
    /// Theme preference.
    /// </summary>
    public string Theme { get; set; } = "System"; // System, Light, Dark, Custom

    /// <summary>
    /// Recently opened files (most recent first).
    /// </summary>
    public List<string> RecentFiles { get; set; } = new();

    /// <summary>
    /// Maximum number of recent files to track.
    /// </summary>
    public int MaxRecentFiles { get; set; } = 10;
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
