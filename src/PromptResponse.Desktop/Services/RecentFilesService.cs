using PromptResponse.Desktop.Models;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Recent-files tracker. When constructed with an <see cref="ISettingsService"/>
/// it loads from and persists to app settings; without one it is in-memory only
/// (the default the view-model falls back to in tests/design time).
/// </summary>
public sealed class RecentFilesService : IRecentFilesService
{
    /// <summary>Maximum entries retained.</summary>
    public const int MaxItems = 8;

    private readonly ISettingsService? _settings;
    private readonly List<RecentFileEntry> _items = new();

    public RecentFilesService(ISettingsService? settings = null)
    {
        _settings = settings;
        if (_settings?.Settings.RecentFiles is { } saved)
        {
            _items.AddRange(saved
                .Where(r => !string.IsNullOrWhiteSpace(r.Path))
                .Select(r => new RecentFileEntry(r.Path, r.Title)));
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<RecentFileEntry> Items => _items;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public void Add(string? path, string? title)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var label = string.IsNullOrWhiteSpace(title) ? Path.GetFileName(path) : title!;

        // Move-to-front semantics: drop any existing entry for the same path.
        _items.RemoveAll(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
        _items.Insert(0, new RecentFileEntry(path, label));

        if (_items.Count > MaxItems)
        {
            _items.RemoveRange(MaxItems, _items.Count - MaxItems);
        }

        Persist();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Persist()
    {
        if (_settings is null)
        {
            return;
        }

        _settings.Settings.RecentFiles = _items
            .Select(i => new RecentFileSetting { Path = i.Path, Title = i.Title })
            .ToList();
        _settings.Save();
    }
}
