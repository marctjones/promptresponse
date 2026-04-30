using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptResponse.Core.Models;
using PromptResponse.Core.Validation;
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
    private readonly DataTypeValidator _dataTypeValidator = new();

    private readonly ObservableCollection<PromptViewModelBase> _promptViewModels = new();
    private readonly ObservableCollection<SectionViewModel> _sections = new();

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
        _profileService.ProfileChanged += (_, _) => OnAllProfileBrushesChanged();
    }

    private void OnAllProfileBrushesChanged()
    {
        OnPropertyChanged(nameof(ActiveProfile));
        OnPropertyChanged(nameof(ActiveProfileSurfaceBrush));
        OnPropertyChanged(nameof(ActiveProfileSubtleSurfaceBrush));
        OnPropertyChanged(nameof(ActiveProfileElevatedSurfaceBrush));
        OnPropertyChanged(nameof(ActiveProfileOnSurfaceBrush));
        OnPropertyChanged(nameof(ActiveProfileMutedTextBrush));
        OnPropertyChanged(nameof(ActiveProfilePrimaryBrush));
        OnPropertyChanged(nameof(ActiveProfileOnPrimaryBrush));
        OnPropertyChanged(nameof(ActiveProfileBorderBrush));
        OnPropertyChanged(nameof(ActiveProfileFocusBrush));
        OnPropertyChanged(nameof(BodyFontSize));
        OnPropertyChanged(nameof(CaptionFontSize));
        OnPropertyChanged(nameof(SubtitleFontSize));
        OnPropertyChanged(nameof(TitleFontSize));
        OnPropertyChanged(nameof(DisplayFontSize));
    }

    /// <summary>The active profile, exposed for view bindings (e.g., font scale, contrast).</summary>
    public IRenderingProfile ActiveProfile => _profileService.ActiveProfile;

    /// <summary>Profile service exposed for the Display Preferences bindings.</summary>
    public IProfileService ProfileService => _profileService;

    private ColorPalette Palette => ColorTokens.For(ActiveProfile.ColorScheme);
    private IBrush BrushFor(ColorRole role) => new SolidColorBrush(Palette[role]);

    /// <summary>Window background — base surface for the active profile.</summary>
    public IBrush ActiveProfileSurfaceBrush => BrushFor(ColorRole.Surface);
    /// <summary>Sidebar / status-bar tint, subtly distinct from the main surface.</summary>
    public IBrush ActiveProfileSubtleSurfaceBrush => BrushFor(ColorRole.SubtleSurface);
    /// <summary>Card / dialog background — sits visually above the main surface.</summary>
    public IBrush ActiveProfileElevatedSurfaceBrush => BrushFor(ColorRole.ElevatedSurface);
    /// <summary>Body-text foreground.</summary>
    public IBrush ActiveProfileOnSurfaceBrush => BrushFor(ColorRole.OnSurface);
    /// <summary>Secondary / muted text foreground.</summary>
    public IBrush ActiveProfileMutedTextBrush => BrushFor(ColorRole.MutedText);
    /// <summary>Action-affordance accent (system blue / system tint).</summary>
    public IBrush ActiveProfilePrimaryBrush => BrushFor(ColorRole.Primary);
    /// <summary>Foreground rendered on Primary.</summary>
    public IBrush ActiveProfileOnPrimaryBrush => BrushFor(ColorRole.OnPrimary);
    /// <summary>Hairline / separator brush.</summary>
    public IBrush ActiveProfileBorderBrush => BrushFor(ColorRole.Border);
    /// <summary>Focus-indicator brush (3px ring under HighContrast, 2px otherwise).</summary>
    public IBrush ActiveProfileFocusBrush => BrushFor(ColorRole.Focus);

    /// <summary>Typography scale, profile-aware. LargeText profile multiplies all sizes by 1.5.</summary>
    public double CaptionFontSize  => 12 * ActiveProfile.TextScale;
    public double BodyFontSize     => 14 * ActiveProfile.TextScale;
    public double SubtitleFontSize => 18 * ActiveProfile.TextScale;
    public double TitleFontSize    => 22 * ActiveProfile.TextScale;
    public double DisplayFontSize  => 32 * ActiveProfile.TextScale;

    public FormProgressViewModel Progress { get; }
    public SearchViewModel Search { get; }
    public IReadOnlyList<PromptViewModelBase> PromptViewModels => _promptViewModels;

    /// <summary>Top-level sections (each carries nested sections + typed prompt VMs).</summary>
    public IReadOnlyList<SectionViewModel> Sections => _sections;

    public bool HasDocument => _session.HasDocument;
    public bool IsFilledForm => _session.Mode == DocumentMode.FillingForm;
    public bool IsEmptyState => !HasDocument;
    public DocumentMode Mode => _session.Mode;
    public string Title => _session.Title;
    public string CurrentDocumentTitle => _session.CurrentDocument?.Metadata.Title ?? string.Empty;
    public string? DocumentDescription => _session.CurrentDocument?.Metadata.Description;
    public bool HasDocumentDescription => !string.IsNullOrWhiteSpace(DocumentDescription);

    /// <summary>Count of advisory warnings from the data-type validator (never errors — vision invariant).</summary>
    public int AdvisoryCount { get; private set; }
    public bool HasAdvisories => AdvisoryCount > 0;
    public string AdvisorySummary => AdvisoryCount switch
    {
        0 => "No advisories",
        1 => "1 advisory",
        _ => $"{AdvisoryCount} advisories",
    };

    /// <summary>
    /// Re-runs the advisory inspection over the current document. Per the vision,
    /// these are never blocking — they're hints surfaced for the user's awareness
    /// (e.g., "five" in a number-hinted field renders an advisory but is still valid).
    /// </summary>
    public void RefreshAdvisories()
    {
        var doc = _session.CurrentDocument;
        if (doc == null)
        {
            AdvisoryCount = 0;
        }
        else
        {
            var result = _dataTypeValidator.ValidateDocument(doc);
            AdvisoryCount = result.Warnings.Count;
        }
        OnPropertyChanged(nameof(AdvisoryCount));
        OnPropertyChanged(nameof(HasAdvisories));
        OnPropertyChanged(nameof(AdvisorySummary));
    }

    /// <summary>
    /// "Filled by Alex Doe on 2025-04-29" style summary — null when the document is
    /// a template or doesn't carry FilledBy metadata.
    /// </summary>
    public string? FilledByDisplay
    {
        get
        {
            var meta = _session.CurrentDocument?.Metadata;
            if (meta == null || _session.Mode != DocumentMode.FillingForm) return null;
            var by = string.IsNullOrWhiteSpace(meta.FilledBy) ? null : meta.FilledBy;
            var when = meta.FilledDate?.ToString("MMMM d, yyyy");
            return (by, when) switch
            {
                (not null, not null) => $"Filled by {by} on {when}",
                (not null, null) => $"Filled by {by}",
                (null, not null) => $"Filled on {when}",
                _ => null,
            };
        }
    }

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

        // Dispose the previous prompt VMs and rebuild the section tree.
        foreach (var vm in _promptViewModels)
        {
            vm.Dispose();
        }
        _promptViewModels.Clear();
        _sections.Clear();

        if (document != null)
        {
            foreach (var section in document.Sections)
            {
                var sectionVm = new SectionViewModel(section, _factory, depth: 0);
                _sections.Add(sectionVm);
                CollectPrompts(sectionVm);
            }
        }

        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(IsEmptyState));
        OnPropertyChanged(nameof(Mode));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(CurrentDocumentTitle));
        OnPropertyChanged(nameof(DocumentDescription));
        OnPropertyChanged(nameof(HasDocumentDescription));
        OnPropertyChanged(nameof(FilledByDisplay));
        OnPropertyChanged(nameof(IsFilledForm));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(PromptViewModels));
        OnPropertyChanged(nameof(Sections));
        RefreshAdvisories();
        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
        CloseCommand.NotifyCanExecuteChanged();
    }

    private void CollectPrompts(SectionViewModel section)
    {
        foreach (var prompt in section.PromptViewModels) _promptViewModels.Add(prompt);
        foreach (var nested in section.NestedSections) CollectPrompts(nested);
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
