namespace PromptResponse.Desktop.Services;

/// <summary>
/// Tracks the most-recently opened/saved documents for the home screen.
/// </summary>
public interface IRecentFilesService
{
    /// <summary>The recent files, most-recent-first.</summary>
    IReadOnlyList<RecentFileEntry> Items { get; }

    /// <summary>
    /// Records a file as the most recent (de-duplicating by path and capping the
    /// list). A blank path is ignored. Persists if backed by settings.
    /// </summary>
    void Add(string? path, string? title);

    /// <summary>Raised whenever <see cref="Items"/> changes.</summary>
    event EventHandler? Changed;
}

/// <summary>A recent-file entry: where it is and how to label it.</summary>
/// <param name="Path">Absolute file path.</param>
/// <param name="Title">Display label (document title, or file name fallback).</param>
public sealed record RecentFileEntry(string Path, string Title);
