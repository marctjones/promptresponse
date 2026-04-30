using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// Thin replacement for the legacy MainWindowViewModel. Composes the focused
/// services and child VMs that own their slice of state. Source-generated INPC
/// + RelayCommand via CommunityToolkit.Mvvm.
/// </summary>
public sealed partial class MainShellViewModel : ObservableObject, IDisposable
{
    private readonly IFileService _fileService;
    private readonly IDialogService _dialogService;
    private readonly IDocumentSessionService _session;
    private readonly IProfileService _profileService;
    private readonly PromptViewModelFactory _factory;

    private readonly ObservableCollection<PromptViewModelBase> _promptViewModels = new();

    public MainShellViewModel(
        IFileService fileService,
        IDialogService dialogService,
        IDocumentSessionService session,
        IProfileService profileService,
        PromptViewModelFactory factory)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));

        Progress = new FormProgressViewModel();
        Search = new SearchViewModel();

        _session.DocumentChanged += OnDocumentChanged;
        _session.DirtyChanged += OnDirtyChanged;
        _profileService.ProfileChanged += (_, _) => OnPropertyChanged(nameof(ActiveProfile));
    }

    /// <summary>The active profile, exposed for view bindings (e.g., font scale, contrast).</summary>
    public IRenderingProfile ActiveProfile => _profileService.ActiveProfile;

    /// <summary>Profile service exposed for the Display Preferences bindings.</summary>
    public IProfileService ProfileService => _profileService;

    public FormProgressViewModel Progress { get; }
    public SearchViewModel Search { get; }
    public IReadOnlyList<PromptViewModelBase> PromptViewModels => _promptViewModels;

    public bool HasDocument => _session.HasDocument;
    public bool IsEmptyState => !HasDocument;
    public DocumentMode Mode => _session.Mode;
    public string Title => _session.Title;
    public string CurrentDocumentTitle => _session.CurrentDocument?.Metadata.Title ?? string.Empty;

    public string StatusMessage => _session.HasDocument
        ? $"{_session.CurrentDocument!.Metadata.Title} — {Progress.StatusText}"
        : "No document open. Use File → New, or File → Open to get started.";

    [RelayCommand]
    private void NewTemplate()
    {
        var doc = new AprDocument
        {
            Version = "1.0",
            DocumentType = DocumentType.Template,
            Metadata = new Metadata
            {
                Title = "New Template",
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow,
            },
            Sections = new List<Section>(),
        };
        _fileService.ClearCurrentFilePath();
        _session.Set(doc, filePath: null, dirty: true);
    }

    [RelayCommand]
    public async Task Open()
    {
        var doc = await _fileService.OpenFileAsync();
        if (doc == null) return;
        _session.Set(doc, _fileService.CurrentFilePath, dirty: false);
    }

    [RelayCommand(CanExecute = nameof(HasDocument))]
    public async Task Save()
    {
        if (!_session.HasDocument) return;

        if (string.IsNullOrEmpty(_fileService.CurrentFilePath))
        {
            await _fileService.SaveFileAsAsync(_session.CurrentDocument!);
        }
        else
        {
            await _fileService.SaveFileAsync(_session.CurrentDocument!, _fileService.CurrentFilePath);
        }
        _session.MarkClean();
    }

    [RelayCommand(CanExecute = nameof(HasDocument))]
    public async Task SaveAs()
    {
        if (!_session.HasDocument) return;
        await _fileService.SaveFileAsAsync(_session.CurrentDocument!);
        _session.MarkClean();
    }

    [RelayCommand(CanExecute = nameof(HasDocument))]
    public async Task Close()
    {
        if (_session.IsDirty)
        {
            var ok = await _dialogService.ShowConfirmationAsync(
                "Unsaved changes",
                "You have unsaved changes. Close anyway?");
            if (!ok) return;
        }
        _session.Close();
        _fileService.ClearCurrentFilePath();
    }

    private void OnDocumentChanged(object? sender, AprDocument? document)
    {
        Progress.SetDocument(document);
        Search.SetDocument(document);

        // Dispose the previous prompt VMs and rebuild from the new document.
        foreach (var vm in _promptViewModels)
        {
            vm.Dispose();
        }
        _promptViewModels.Clear();

        if (document != null)
        {
            foreach (var prompt in EnumeratePrompts(document))
            {
                _promptViewModels.Add(_factory.Create(prompt));
            }
        }

        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(IsEmptyState));
        OnPropertyChanged(nameof(Mode));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(CurrentDocumentTitle));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(PromptViewModels));
        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
        CloseCommand.NotifyCanExecuteChanged();
    }

    private void OnDirtyChanged(object? sender, bool isDirty)
    {
        OnPropertyChanged(nameof(Title));
    }

    private static IEnumerable<Prompt> EnumeratePrompts(AprDocument document)
    {
        foreach (var section in document.Sections)
        {
            foreach (var prompt in EnumerateSection(section)) yield return prompt;
        }
    }

    private static IEnumerable<Prompt> EnumerateSection(Section section)
    {
        foreach (var prompt in section.Prompts) yield return prompt;
        foreach (var nested in section.Sections)
        {
            foreach (var prompt in EnumerateSection(nested)) yield return prompt;
        }
    }

    public void Dispose()
    {
        _session.DocumentChanged -= OnDocumentChanged;
        _session.DirtyChanged -= OnDirtyChanged;
        foreach (var vm in _promptViewModels)
        {
            vm.Dispose();
        }
        _promptViewModels.Clear();
    }
}
