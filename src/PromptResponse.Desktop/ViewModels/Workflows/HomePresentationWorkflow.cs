using System.Collections.ObjectModel;
using PromptResponse.Desktop.Services;

namespace PromptResponse.Desktop.ViewModels.Workflows;

/// <summary>
/// Owns the presentation state of the shell's home screen: starter templates,
/// recent files, and the first-run hint.
/// </summary>
internal sealed class HomePresentationWorkflow : IDisposable
{
    private readonly IRecentFilesService _recentFiles;

    public HomePresentationWorkflow(IRecentFilesService recentFiles, ITemplateCatalogService? templateCatalog)
    {
        _recentFiles = recentFiles;
        foreach (var template in templateCatalog?.Templates ?? Array.Empty<StarterTemplate>())
        {
            StarterTemplates.Add(new RecentFileViewModel(template.Path, template.Title));
        }

        _recentFiles.Changed += OnRecentFilesChanged;
        RefreshRecentFiles();
    }

    public ObservableCollection<RecentFileViewModel> StarterTemplates { get; } = new();
    public ObservableCollection<RecentFileViewModel> RecentFiles { get; } = new();
    public bool HasStarterTemplates => StarterTemplates.Count > 0;
    public bool HasRecentFiles => RecentFiles.Count > 0;
    public bool ShowGettingStarted => !HasRecentFiles;

    public event Action? StateChanged;

    public void AddToRecent(string? path, string? title) => _recentFiles.Add(path, title);

    private void OnRecentFilesChanged(object? sender, EventArgs e)
    {
        RefreshRecentFiles();
        StateChanged?.Invoke();
    }

    private void RefreshRecentFiles()
    {
        RecentFiles.Clear();
        foreach (var entry in _recentFiles.Items)
        {
            RecentFiles.Add(new RecentFileViewModel(entry.Path, entry.Title));
        }
    }

    public void Dispose() => _recentFiles.Changed -= OnRecentFilesChanged;
}
