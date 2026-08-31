using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptResponse.Core;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Signing;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels.Editing;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.ViewModels.Workflows;
using PromptResponse.Rendering.Pdf;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// Top-level shell view-model. Composes the focused services and child VMs
/// that each own their slice of state (session, profile, progress, search,
/// advisories). Source-generated INPC + RelayCommand via CommunityToolkit.Mvvm.
/// </summary>
public sealed partial class MainShellViewModel : ObservableObject, IDisposable
{
    private readonly IFileService _fileService;
    private readonly IDialogService _dialogService;
    private readonly IDocumentSessionService _session;
    private readonly IProfileService _profileService;
    private readonly PromptViewModelFactory _factory;
    private readonly DocumentDeliveryWorkflow _deliveryWorkflow;
    private readonly DocumentOutputWorkflow _outputWorkflow;
    private readonly DocumentSessionWorkflow _sessionWorkflow;
    private readonly EditHistory _editHistory;
    private readonly DocumentTreeWorkflow _documentTreeWorkflow;
    private readonly DocumentLifecycleCoordinator _documentLifecycle;
    private readonly DocumentHeaderPresentation _documentHeader;
    private readonly ProfilePresentationState _profilePresentation;
    private readonly HomePresentationWorkflow _homePresentation;
    private readonly AdvisoryWorkflow _advisoryWorkflow = new();
    private readonly ExpressionWorkflow _expressionWorkflow;
    private readonly RoleSelectionWorkflow _roleSelectionWorkflow;
    private IReadOnlyList<AprAttestationResolution> _beta6Attestations = [];
    private readonly WizardProfileWorkflow _wizardProfileWorkflow;

    public MainShellViewModel(
        IFileService fileService,
        IDialogService dialogService,
        IDocumentSessionService session,
        IProfileService profileService,
        PromptViewModelFactory factory,
        EditHistory? editHistory = null,
        IRecentFilesService? recentFiles = null,
        ITemplateCatalogService? templateCatalog = null,
        IAprSerializer? serializer = null,
        IMailHandoffService? mailHandoff = null,
        IHttpsSubmissionService? httpsSubmission = null)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _profilePresentation = new ProfilePresentationState(_profileService);
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        _editHistory = editHistory ?? new EditHistory();
        _homePresentation = new HomePresentationWorkflow(
            recentFiles ?? new RecentFilesService(),
            templateCatalog);
        // Reconstruct the factory locally so it threads the shell's edit history
        // into every prompt VM it creates. The injected factory's profile is the
        // same DI singleton — only its history binding differs.
        _factory = new PromptViewModelFactory(profileService, _editHistory);
        _homePresentation.StateChanged += OnHomePresentationStateChanged;
        _deliveryWorkflow = new DocumentDeliveryWorkflow(
            _session,
            _fileService,
            _dialogService,
            serializer ?? new AprJsonSerializer(),
            mailHandoff ?? new MailHandoffService(),
            httpsSubmission ?? new HttpsSubmissionService(),
            AddToRecent);
        _outputWorkflow = new DocumentOutputWorkflow(_session, _fileService, _dialogService);
        _sessionWorkflow = new DocumentSessionWorkflow(_session, _fileService, _dialogService, AddToRecent);
        _documentTreeWorkflow = new DocumentTreeWorkflow(
            _session,
            _factory,
            _editHistory,
            OnPromptResponseChanged,
            OnDocumentTreeChanged);
        _expressionWorkflow = new ExpressionWorkflow(() => _documentTreeWorkflow.Prompts);
        _roleSelectionWorkflow = new RoleSelectionWorkflow(() => _documentTreeWorkflow.Prompts);
        _wizardProfileWorkflow = new WizardProfileWorkflow(
            _profileService,
            () => _documentTreeWorkflow.Sections.Count,
            index => index >= 0 && index < _documentTreeWorkflow.Sections.Count ? _documentTreeWorkflow.Sections[index].Title : null);
        _documentTreeWorkflow.TreeChanged += _wizardProfileWorkflow.RefreshSections;
        _advisoryWorkflow.StateChanged += OnAdvisoryWorkflowStateChanged;
        _roleSelectionWorkflow.StateChanged += OnRoleSelectionWorkflowStateChanged;
        _wizardProfileWorkflow.StateChanged += OnWizardProfileWorkflowStateChanged;

        Progress = new FormProgressViewModel();
        Search = new SearchViewModel();
        _documentHeader = new DocumentHeaderPresentation(_session, Progress);
        _documentLifecycle = new DocumentLifecycleCoordinator(
            _session,
            _editHistory,
            _documentTreeWorkflow,
            Progress,
            Search,
            _roleSelectionWorkflow,
            _wizardProfileWorkflow,
            _ => ApplyExpressions(),
            editMode => IsEditMode = editMode);
        _documentLifecycle.StateChanged += OnDocumentLifecycleStateChanged;

        _session.DocumentChanged += OnDocumentChanged;
        _session.DirtyChanged += OnDirtyChanged;
        Progress.PropertyChanged += OnProgressPropertyChanged;
        _editHistory.PropertyChanged += OnEditHistoryChanged;
    }

    /// <summary>Bundled starter templates shown on the home screen.</summary>
    public ObservableCollection<RecentFileViewModel> StarterTemplates => _homePresentation.StarterTemplates;

    /// <summary>True when there is at least one starter template to offer.</summary>
    public bool HasStarterTemplates => _homePresentation.HasStarterTemplates;

    /// <summary>Starts a new, unsaved document from a bundled starter template.</summary>
    [RelayCommand]
    public async Task NewFromTemplate(string? path)
    {
        await _sessionWorkflow.NewFromTemplateAsync(path);
    }

    /// <summary>Recently opened/saved files shown on the home screen, most-recent-first.</summary>
    public ObservableCollection<RecentFileViewModel> RecentFiles => _homePresentation.RecentFiles;

    /// <summary>True when there is at least one recent file to offer on the home screen.</summary>
    public bool HasRecentFiles => _homePresentation.HasRecentFiles;

    /// <summary>
    /// First-run onboarding hint: shown on the home screen until the user has
    /// opened or saved something (i.e. while there are no recent files).
    /// </summary>
    public bool ShowGettingStarted => _homePresentation.ShowGettingStarted;

    private void OnHomePresentationStateChanged()
    {
        OnPropertyChanged(nameof(HasRecentFiles));
        OnPropertyChanged(nameof(ShowGettingStarted));
    }

    private void AddToRecent(string? path, string? title) => _homePresentation.AddToRecent(path, title);

    /// <summary>Opens a file chosen from the home screen's recent list.</summary>
    [RelayCommand]
    public async Task OpenRecent(string? path)
    {
        await _sessionWorkflow.OpenRecentAsync(path);
    }

    /// <summary>The shell-owned undo/redo history. Cleared on document load so
    /// edits in one document never resurface in another.</summary>
    public EditHistory EditHistory => _editHistory;

    public bool CanUndo => _editHistory.CanUndo;
    public bool CanRedo => _editHistory.CanRedo;
    public string UndoLabel => _editHistory.UndoDescription is { } d ? $"Undo {d}" : "Undo";
    public string RedoLabel => _editHistory.RedoDescription is { } d ? $"Redo {d}" : "Redo";

    // ── Wizard mode (one-section-at-a-time view) ──

    /// <summary>True when the form should render section-by-section. Bound to
    /// the WizardModeProfile capability flag — toggling either the View menu
    /// item or the underlying profile keeps both in sync. Auto-on for the
    /// Cognitive preset; user-overridable any time.</summary>
    public bool IsWizardMode => _wizardProfileWorkflow.IsWizardMode;

    /// <summary>Active section index in wizard mode. Reset to 0 on every
    /// document load so a fresh document starts at section 1.</summary>
    public int WizardSectionIndex
    {
        get => _wizardProfileWorkflow.SectionIndex;
        set => _wizardProfileWorkflow.SetSectionIndex(value);
    }

    /// <summary>The currently-visible section in wizard mode, or null if no
    /// document is open / no sections exist. Bound by the wizard content area.</summary>
    public SectionViewModel? WizardCurrentSection =>
        _wizardProfileWorkflow.HasCurrentSection
            ? _documentTreeWorkflow.Sections[WizardSectionIndex]
            : null;

    /// <summary>"Section 3 of 12: Employment History" header for the wizard
    /// nav bar. Empty string when no document is open.</summary>
    public string WizardStepLabel => _wizardProfileWorkflow.StepLabel;

    public bool CanWizardPrevious => _wizardProfileWorkflow.CanPrevious;
    public bool CanWizardNext => _wizardProfileWorkflow.CanNext;

    /// <summary>True when the full-list edit-mode view should render — edit
    /// mode AND wizard mode off.</summary>
    public bool ShowFullEditList => IsEditMode && !IsWizardMode;

    /// <summary>True when the full-list fill-mode view should render — fill
    /// mode AND wizard mode off.</summary>
    public bool ShowFullFillList => !IsEditMode && !IsWizardMode;

    [RelayCommand(CanExecute = nameof(CanWizardPrevious))]
    private void WizardPrevious() => _wizardProfileWorkflow.MovePrevious();

    [RelayCommand(CanExecute = nameof(CanWizardNext))]
    private void WizardNext() => _wizardProfileWorkflow.MoveNext();

    /// <summary>Jump directly to a specific section index (used by sidebar +
    /// programmatic moves). Out-of-range values are clamped.</summary>
    public void JumpToWizardSection(int index)
    {
        _wizardProfileWorkflow.SetSectionIndex(index);
    }

    /// <summary>Toggle wizard mode via the View menu. Flips the underlying
    /// WizardModeProfile flag so the change is observable everywhere (DisplayPreferences,
    /// active-flag summary, persistence).</summary>
    [RelayCommand]
    private void ToggleWizardMode()
    {
        _wizardProfileWorkflow.ToggleWizardMode();
    }

    partial void OnIsEditModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowFullEditList));
        OnPropertyChanged(nameof(ShowFullFillList));
        SubmitViaEmailCommand.NotifyCanExecuteChanged();
    }

    /// <summary>One-click capability-profile preset switch. Bound to the View →
    /// Capability Profile submenu items; the parameter is the preset enum name
    /// (e.g. "ExcellentVision") so each menu item can pass its preset via
    /// <c>CommandParameter</c>. Apply composes the preset's flag set on top of
    /// a clean baseline; existing toggles are reset.</summary>
    [RelayCommand]
    private void ApplyPreset(string? presetName)
    {
        _wizardProfileWorkflow.ApplyPreset(presetName);
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => _editHistory.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => _editHistory.Redo();

    private void OnEditHistoryChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoLabel));
        OnPropertyChanged(nameof(RedoLabel));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void OnWizardProfileWorkflowStateChanged(WizardProfileChange change)
    {
        if (change.HasFlag(WizardProfileChange.Navigation))
            RefreshWizardDerived();
        if (change.HasFlag(WizardProfileChange.ProfilePresentation))
            OnAllProfileBrushesChanged();
    }

    private void OnAllProfileBrushesChanged()
    {
        OnPropertyChanged(nameof(ActiveProfile));
        OnPropertyChanged(nameof(ActiveThemeVariant));
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
        // Wizard mode is profile-flag-driven; toggle propagates here on any
        // profile change (preset apply, View menu toggle, persisted state).
        OnPropertyChanged(nameof(IsWizardMode));
        OnPropertyChanged(nameof(ShowFullEditList));
        OnPropertyChanged(nameof(ShowFullFillList));
    }

    /// <summary>The active profile, exposed for view bindings (e.g., font scale, contrast).</summary>
    public IRenderingProfile ActiveProfile => _profilePresentation.ActiveProfile;

    /// <summary>Profile service exposed for the Display Preferences bindings.</summary>
    public IProfileService ProfileService => _profileService;

    /// <summary>The file service, for dialogs the view owns that need a save picker.</summary>
    public IFileService FileServiceForDialogs => _fileService;

    /// <summary>Window background — base surface for the active profile.</summary>
    public IBrush ActiveProfileSurfaceBrush => _profilePresentation.SurfaceBrush;
    /// <summary>Sidebar / status-bar tint, subtly distinct from the main surface.</summary>
    public IBrush ActiveProfileSubtleSurfaceBrush => _profilePresentation.SubtleSurfaceBrush;
    /// <summary>Card / dialog background — sits visually above the main surface.</summary>
    public IBrush ActiveProfileElevatedSurfaceBrush => _profilePresentation.ElevatedSurfaceBrush;
    /// <summary>Body-text foreground.</summary>
    public IBrush ActiveProfileOnSurfaceBrush => _profilePresentation.OnSurfaceBrush;
    /// <summary>Secondary / muted text foreground.</summary>
    public IBrush ActiveProfileMutedTextBrush => _profilePresentation.MutedTextBrush;
    /// <summary>Action-affordance accent (system blue / system tint).</summary>
    public IBrush ActiveProfilePrimaryBrush => _profilePresentation.PrimaryBrush;
    /// <summary>Foreground rendered on Primary.</summary>
    public IBrush ActiveProfileOnPrimaryBrush => _profilePresentation.OnPrimaryBrush;
    /// <summary>Hairline / separator brush.</summary>
    public IBrush ActiveProfileBorderBrush => _profilePresentation.BorderBrush;

    /// <summary>Hairline between regions. Quieter than a component outline.</summary>
    public IBrush ActiveProfileDividerBrush => _profilePresentation.DividerBrush;

    // ── Shape and density tokens ──
    //
    // Radii lived inline in the XAML and had drifted to six different values, the largest
    // of them 12. Large radii read as consumer software; desktop applications people work
    // in all day sit at two to four. Named here so the answer is in one place and the
    // views stop each choosing their own.

    /// <summary>Corner radius for inputs, buttons and other controls.</summary>
    public CornerRadius ControlCornerRadius => _profilePresentation.ControlCornerRadius;

    /// <summary>Corner radius for cards and grouped regions.</summary>
    public CornerRadius SurfaceCornerRadius => _profilePresentation.SurfaceCornerRadius;
    /// <summary>Focus-indicator brush (3px ring under HighContrast, 2px otherwise).</summary>
    public IBrush ActiveProfileFocusBrush => _profilePresentation.FocusBrush;

    /// <summary>
    /// FluentTheme variant matching the active profile's color scheme. Bound to
    /// Window.RequestedThemeVariant so child controls (RadioButton, CheckBox,
    /// MenuItem) inherit FluentTheme colors that contrast with our profile palette.
    /// </summary>
    public ThemeVariant ActiveThemeVariant => _profilePresentation.ThemeVariant;

    /// <summary>Typography scale, profile-aware. LargeText profile multiplies all sizes by 1.5.</summary>
    public double CaptionFontSize => _profilePresentation.CaptionFontSize;
    public double BodyFontSize => _profilePresentation.BodyFontSize;
    public double SubtitleFontSize => _profilePresentation.SubtitleFontSize;
    public double TitleFontSize => _profilePresentation.TitleFontSize;
    public double DisplayFontSize => _profilePresentation.DisplayFontSize;

    public FormProgressViewModel Progress { get; }
    public SearchViewModel Search { get; }
    public IReadOnlyList<PromptViewModelBase> PromptViewModels => _documentTreeWorkflow.Prompts;

    /// <summary>Top-level sections (each carries nested sections + typed prompt VMs).</summary>
    public IReadOnlyList<SectionViewModel> Sections => _documentTreeWorkflow.Sections;

    public bool HasDocument => _documentHeader.HasDocument;
    public bool IsFilledForm => _documentHeader.IsFilledForm;
    public bool IsEditingTemplate => _documentHeader.IsEditingTemplate;
    public bool IsEmptyState => _documentHeader.IsEmptyState;
    public DocumentMode Mode => _documentHeader.Mode;

    /// <summary>The mode, in words rather than as an enum member name.</summary>
    /// <remarks>
    /// The sidebar bound DocumentMode directly and displayed "EditingTemplate" to the
    /// user — an identifier from the source leaking into the interface. Only visible by
    /// looking at a rendered frame.
    /// </remarks>
    public string ModeDescription => _documentHeader.ModeDescription;
    public string Title => _documentHeader.Title;

    /// <summary>
    /// Template authoring mode. When true, the shell renders the structural editor
    /// (SectionEditorView) so the user can add/remove/rename sections and prompts.
    /// When false, the shell renders the fillable form (SectionView). Filled forms
    /// are always in fill mode; templates default to edit mode but can toggle to
    /// preview-fill via the View menu.
    /// </summary>
    [ObservableProperty]
    private bool _isEditMode;

    /// <summary>True when the toggle is meaningful — only templates can switch
    /// between edit and preview. Filled forms stay in fill mode.</summary>
    public bool CanToggleEditMode => HasDocument && IsEditingTemplate;

    [RelayCommand]
    private void ToggleEditMode()
    {
        if (!CanToggleEditMode) return;
        IsEditMode = !IsEditMode;
    }
    public string CurrentDocumentTitle => _documentHeader.CurrentDocumentTitle;
    public string? DocumentDescription => _documentHeader.DocumentDescription;
    public bool HasDocumentDescription => _documentHeader.HasDocumentDescription;

    /// <summary>Editable wrapper around the active document's metadata. Bound by
    /// the edit-mode metadata panel; null when no document is open. When the user
    /// types in any metadata field, this VM raises Changed so the shell marks
    /// the document dirty and refreshes derived display properties.</summary>
    public DocumentMetadataViewModel? Metadata => _documentLifecycle.Metadata;

    /// <summary>Count of advisory warnings from the data-type validator (never errors — vision invariant).</summary>
    public int AdvisoryCount => _advisoryWorkflow.Count;
    public bool HasAdvisories => _advisoryWorkflow.Count > 0;
    public string AdvisorySummary => _advisoryWorkflow.Count switch
    {
        0 => "No advisories",
        1 => "1 advisory",
        _ => $"{_advisoryWorkflow.Count} advisories",
    };

    /// <summary>Itemized list of advisories. Each entry links back to the prompt that
    /// triggered it (PromptId, PromptLabel) and explains why (Message). Pinned in the
    /// right rail so the user can see which fields need attention without hunting.</summary>
    public IReadOnlyList<AdvisoryItem> Advisories => _advisoryWorkflow.Items;

    /// <summary>
    /// Raised when the user activates an advisory to jump to the field it refers
    /// to. The view handles the actual scroll + focus (it owns the visual tree).
    /// </summary>
    public event Action<string>? FocusPromptRequested;

    /// <summary>Raised when the user asks to make a signing key.</summary>
    /// <remarks>The view owns the dialog; the shell only asks for it.</remarks>
    public event Action? CreateSigningKeyRequested;

    /// <summary>File &gt; Sign &gt; Create a signing key.</summary>
    /// <remarks>
    /// Always available, with or without a document open: needing a key is usually what
    /// somebody discovers when they first try to sign, and making them open a file first
    /// would be arbitrary.
    /// </remarks>
    [RelayCommand]
    private void CreateSigningKey() => CreateSigningKeyRequested?.Invoke();

    /// <summary>Raised when the user asks to leave the application.</summary>
    /// <remarks>
    /// The view closes the window in response rather than the model shutting down
    /// directly, so the window's own Closing handling still runs.
    /// </remarks>
    public event Action? ExitRequested;

    /// <summary>File &gt; Exit.</summary>
    /// <remarks>
    /// A command rather than a Click handler, because a menu item wired to a handler is
    /// invisible to anything that inspects the menu - which is exactly what
    /// EveryFileMenuItem_HasACommandBinding checks, and it caught the first attempt.
    /// </remarks>
    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke();

    /// <summary>Navigates to the field an advisory refers to (click-to-field).</summary>
    [RelayCommand]
    public void FocusAdvisory(string? promptId)
    {
        if (!string.IsNullOrEmpty(promptId))
        {
            FocusPromptRequested?.Invoke(promptId);
        }
    }

    /// <summary>Whether the open beta.6 stream carries independent attestations.</summary>
    public bool HasBeta6Attestations => _beta6Attestations.Count > 0;

    /// <summary>One-line verification summary for the independent beta.6 records.</summary>
    public string Beta6AttestationSummary => _beta6Attestations.Count == 0 ? "No independent attestations" :
        $"{_beta6Attestations.Count} independent attestation(s): {string.Join(", ", _beta6Attestations.GroupBy(item => item.State).OrderBy(group => group.Key).Select(group => $"{group.Count()} {group.Key.ToString().ToLowerInvariant()}"))}";

    private void RefreshBeta6Attestations()
    {
        _beta6Attestations = _fileService.GetBeta6Attestations();
        OnPropertyChanged(nameof(HasBeta6Attestations));
        OnPropertyChanged(nameof(Beta6AttestationSummary));
    }

    /// <summary>
    /// Appends a detached beta.6 document attestation using a chosen PFX certificate.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    public async Task SignAsPublisher()
    {
        await AppendBeta6AttestationAsync(null);
    }

    /// <summary>
    /// Appends a detached beta.6 fields attestation for answered responses.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    public async Task SignMyResponses()
    {
        var fields = _documentTreeWorkflow.Prompts.Where(prompt => !string.IsNullOrWhiteSpace(prompt.Response))
            .Select(prompt => prompt.Id).ToList();
        if (fields.Count == 0)
        {
            await _dialogService.ShowConfirmationAsync("Nothing to attest", "Fill in at least one response before creating a fields attestation.");
            return;
        }
        await AppendBeta6AttestationAsync(fields);
    }

    private async Task AppendBeta6AttestationAsync(IReadOnlyList<string>? fields)
    {
        var document = _session.CurrentDocument;
        if (document is null) return;
        var certificatePath = await _fileService.PickCertificateAsync();
        if (string.IsNullOrEmpty(certificatePath)) return;
        var password = await _dialogService.ShowInputAsync("Certificate password", "Enter the certificate password (leave blank if none):", string.Empty, isPassword: true);
        if (password is null) return;
        try
        {
            using var certificate = SignatureCertificates.LoadPfx(certificatePath, string.IsNullOrEmpty(password) ? null : password);
            if (!await _fileService.AppendBeta6AttestationAsync(document, certificate, fields))
            {
                await _dialogService.ShowConfirmationAsync("Save the form first", "Beta.6 attestations are appended to a saved stream. Save this form, then try again.");
                return;
            }
            await _dialogService.ShowConfirmationAsync("Attestation added", "A detached beta.6 CMS attestation was appended to the stream. It does not modify the form.");
            RefreshBeta6Attestations();
        }
        catch (Exception exception)
        {
            await _dialogService.ShowConfirmationAsync("Attestation failed", exception.Message);
        }
    }

    /// <summary>
    /// Re-runs the advisory inspection over the current document. Per the vision,
    /// these are never blocking — they're hints surfaced for the user's awareness
    /// (e.g., "five" in a number-hinted field renders an advisory but is still valid).
    /// </summary>
    public void RefreshAdvisories()
    {
        _advisoryWorkflow.Refresh(_session.CurrentDocument);
    }

    private void OnAdvisoryWorkflowStateChanged()
    {
        OnPropertyChanged(nameof(AdvisoryCount));
        OnPropertyChanged(nameof(HasAdvisories));
        OnPropertyChanged(nameof(AdvisorySummary));
        OnPropertyChanged(nameof(Advisories));
    }

    /// <summary>
    /// "Filled by Alex Doe on 2025-04-29" style summary — null when the document is
    /// a template or doesn't carry FilledBy metadata.
    /// </summary>
    public string? FilledByDisplay => _documentHeader.FilledByDisplay;

    public string StatusMessage => _documentHeader.StatusMessage;

    [RelayCommand]
    private void NewTemplate()
    {
        // Seed a starter section so the document validates and the editor has
        // something to render. The user renames / adds prompts from there.
        var doc = new AprDocument
        {
            Version = AprFormat.CurrentVersion,
            DocumentType = DocumentType.Template,
            Metadata = new Metadata
            {
                Title = "New Template",
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow,
            },
            Sections = new List<Section>
            {
                new()
                {
                    Id = $"section_{Guid.NewGuid():N}",
                    Title = "Section 1",
                    Prompts = new List<Prompt>
                    {
                        new()
                        {
                            Id = $"prompt_{Guid.NewGuid():N}",
                            Label = "First prompt",
                            Hints = new PromptHints { ExpectedDataType = "text" },
                        },
                    },
                },
            },
        };
        _fileService.ClearCurrentFilePath();
        _session.Set(doc, filePath: null, dirty: true);
    }

    /// <summary>
    /// Append a new top-level section to the current document. Used by the
    /// editor's "+ Add top-level section" button. Top-level sections live on
    /// the document directly, so the shell — not any SectionViewModel — owns
    /// this command.
    /// </summary>
    [RelayCommand]
    public void AddTopLevelSection()
        => _documentTreeWorkflow.AddTopLevelSection();

    /// <summary>Remove a top-level section from the current document. The user
    /// triggers this via the editor's section-list ✕ button. Disposes any prompt
    /// VMs under the removed subtree so subscriptions and resources are released.</summary>
    [RelayCommand]
    public void RemoveTopLevelSection(SectionViewModel? sectionVm)
        => _documentTreeWorkflow.RemoveTopLevelSection(sectionVm);

    /// <summary>Reorder a top-level section. Undoable.</summary>
    public void MoveTopLevelSection(int fromIndex, int toIndex)
        => _documentTreeWorkflow.MoveTopLevelSection(fromIndex, toIndex);

    private void RefreshWizardDerived()
    {
        OnPropertyChanged(nameof(WizardSectionIndex));
        OnPropertyChanged(nameof(WizardCurrentSection));
        OnPropertyChanged(nameof(WizardStepLabel));
        OnPropertyChanged(nameof(CanWizardPrevious));
        OnPropertyChanged(nameof(CanWizardNext));
        WizardPreviousCommand.NotifyCanExecuteChanged();
        WizardNextCommand.NotifyCanExecuteChanged();
    }


    [RelayCommand]
    public async Task Open()
    {
        await _sessionWorkflow.OpenAsync();
    }

    /// <summary>
    /// Loads a document directly from disk without showing the file picker. Used by
    /// the command-line "--open path" flag and demo flows.
    /// </summary>
    public async Task OpenFromPath(string filePath, bool openForFilling = false)
    {
        if (!await _sessionWorkflow.OpenFromPathAsync(filePath)) return;
        // A template normally opens in its editor, but command-line --open is
        // explicitly the form-filling entry point. Keep the document a template
        // until saved while presenting its prompts as fields rather than authoring
        // controls.
        if (openForFilling) IsEditMode = false;
    }

    [RelayCommand(CanExecute = nameof(HasDocument))]
    public async Task Save()
    {
        await _sessionWorkflow.SaveAsync();
    }

    [RelayCommand(CanExecute = nameof(HasDocument))]
    public async Task SaveAs()
    {
        await _sessionWorkflow.SaveAsAsync();
    }

    /// <summary>
    /// Saves a filled copy and opens a draft addressed to one explicit mailto: target.
    /// This never sends mail and never changes the currently open document's path.
    /// </summary>
    public bool CanSubmitViaEmail() => _deliveryWorkflow.CanSubmitViaEmail(IsEditMode);

    [RelayCommand(CanExecute = nameof(CanSubmitViaEmail))]
    public Task SubmitViaEmail() => _deliveryWorkflow.SubmitViaEmailAsync();

    public bool CanSubmitViaHttps() => _deliveryWorkflow.CanSubmitViaHttps(IsEditMode);

    [RelayCommand(CanExecute = nameof(CanSubmitViaHttps))]
    public Task SubmitViaHttps() => _deliveryWorkflow.SubmitViaHttpsAsync();

    /// <summary>
    /// Imports a fillable PDF (AcroForm) into a new untitled template and opens it.
    /// The result is marked dirty so the user is prompted to Save As.
    /// </summary>
    [RelayCommand]
    public async Task ImportPdf()
    {
        await _sessionWorkflow.ImportPdfAsync();
    }

    /// <summary>Exports the currently open document — with its current values — to a flat PDF.</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    public Task ExportPdf() => _outputWorkflow.ExportPdfAsync(fillable: false);

    /// <summary>Shows an in-app preview of the generated print/PDF content.</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    public async Task PrintPreview()
    {
        await _outputWorkflow.ShowPrintPreviewAsync();
    }

    /// <summary>Exports the currently open document to a fillable AcroForm PDF.</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    public Task ExportPdfForm() => _outputWorkflow.ExportPdfAsync(fillable: true);

    /// <summary>Exports the current document — with its values — to a read-only HTML page.</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    public Task ExportHtml() => _outputWorkflow.ExportHtmlAsync(fillable: false);

    /// <summary>Exports the current document to a self-contained, fillable HTML web form.</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    public Task ExportHtmlForm() => _outputWorkflow.ExportHtmlAsync(fillable: true);

    [RelayCommand(CanExecute = nameof(HasDocument))]
    public async Task Close()
    {
        await _sessionWorkflow.CloseAsync();
    }

    private void OnDocumentChanged(object? sender, AprDocument? document)
    {
        _documentLifecycle.HandleDocumentChanged(document);
    }

    private void OnDocumentLifecycleStateChanged(DocumentLifecycleChange change)
    {
        if (change.HasFlag(DocumentLifecycleChange.Metadata))
        {
            OnPropertyChanged(nameof(CurrentDocumentTitle));
            OnPropertyChanged(nameof(DocumentDescription));
            OnPropertyChanged(nameof(HasDocumentDescription));
            OnPropertyChanged(nameof(Title));
            return;
        }

        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(IsEmptyState));
        OnPropertyChanged(nameof(Mode));
        OnPropertyChanged(nameof(ModeDescription));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(CurrentDocumentTitle));
        OnPropertyChanged(nameof(DocumentDescription));
        OnPropertyChanged(nameof(HasDocumentDescription));
        OnPropertyChanged(nameof(FilledByDisplay));
        OnPropertyChanged(nameof(IsFilledForm));
        OnPropertyChanged(nameof(IsEditingTemplate));
        OnPropertyChanged(nameof(CanToggleEditMode));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(PromptViewModels));
        OnPropertyChanged(nameof(Sections));
        OnPropertyChanged(nameof(Metadata));
        ToggleEditModeCommand.NotifyCanExecuteChanged();
        RefreshAdvisories();
        RefreshBeta6Attestations();
        SignAsPublisherCommand.NotifyCanExecuteChanged();
        SignMyResponsesCommand.NotifyCanExecuteChanged();
        PrintPreviewCommand.NotifyCanExecuteChanged();
        ExportPdfCommand.NotifyCanExecuteChanged();
        ExportPdfFormCommand.NotifyCanExecuteChanged();
        ExportHtmlCommand.NotifyCanExecuteChanged();
        ExportHtmlFormCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
        SubmitViaEmailCommand.NotifyCanExecuteChanged();
        SubmitViaHttpsCommand.NotifyCanExecuteChanged();
        CloseCommand.NotifyCanExecuteChanged();
    }

    // ── Roles (specification 4.10) ──

    /// <summary>The roles this form uses, plus "everyone" for showing the whole thing.</summary>
    /// <remarks>
    /// Empty for a single-party form, which is most of them, so the picker never appears
    /// where it would only be clutter.
    /// </remarks>
    public ObservableCollection<RoleChoice> AvailableRoles => _roleSelectionWorkflow.AvailableRoles;

    /// <summary>Whether this form is filled by more than one party.</summary>
    public bool HasRoles => _roleSelectionWorkflow.HasRoles;

    /// <summary>
    /// Which role the person at the keyboard says they are filling.
    /// </summary>
    /// <remarks>
    /// Choosing a role changes only what is emphasised. Nothing is hidden and nothing is
    /// disabled: the format cannot know who is actually there, and a nurse filling in for
    /// reception must not be stopped by a dropdown. The point is that "is this one mine?"
    /// is answered by the form rather than by asking somebody.
    /// </remarks>
    public RoleChoice? ActiveRoleChoice
    {
        get => _roleSelectionWorkflow.ActiveRoleChoice;
        set => _roleSelectionWorkflow.ActiveRoleChoice = value;
    }

    /// <summary>The author's sentence about the selected role, when they wrote one.</summary>
    public string? ActiveRoleDescription => _roleSelectionWorkflow.ActiveRoleDescription;

    /// <summary>What the shell says about the current selection.</summary>
    public string ActiveRoleSummary => _roleSelectionWorkflow.ActiveRoleSummary;

    private void OnRoleSelectionWorkflowStateChanged()
    {
        OnPropertyChanged(nameof(HasRoles));
        OnPropertyChanged(nameof(ActiveRoleChoice));
        OnPropertyChanged(nameof(ActiveRoleDescription));
        OnPropertyChanged(nameof(ActiveRoleSummary));
    }

    private void OnDocumentTreeChanged()
    {
        OnPropertyChanged(nameof(PromptViewModels));
        OnPropertyChanged(nameof(Sections));
        Progress.Refresh();
        RefreshAdvisories();
    }

    /// <summary>
    /// Pulses progress + advisories whenever any prompt VM's Response changes.
    /// Filtered to Response-only updates so other property pulses (DisplayValue,
    /// Show* derived bools) don't trigger redundant validation passes.
    /// </summary>
    private void OnPromptResponseChanged(PromptViewModelBase _, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PromptViewModelBase.Response)) return;
        if (_expressionWorkflow.IsApplying) return;   // ignore the cascade from ApplyExpressions itself
        ApplyExpressions();
        Progress.Refresh();
        RefreshAdvisories();

    }

    /// <summary>
    /// Evaluates the document's expression hints against the current responses:
    /// recomputes computed (<c>exprValue</c>) fields, then sets each prompt VM's
    /// visibility (<c>exprHidden</c>) and read-only (<c>exprValue</c>/<c>exprReadOnly</c>)
    /// state. Re-entrancy-guarded since recompute writes back into the model.
    /// </summary>
    public void ApplyExpressions()
    {
        _expressionWorkflow.Apply(_session.CurrentDocument);
    }

    private void OnDirtyChanged(object? sender, bool isDirty)
    {
        OnPropertyChanged(nameof(Title));
    }

    private void OnProgressPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FormProgressViewModel.StatusText))
            OnPropertyChanged(nameof(StatusMessage));
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
        Progress.PropertyChanged -= OnProgressPropertyChanged;
        _homePresentation.StateChanged -= OnHomePresentationStateChanged;
        _homePresentation.Dispose();
        _documentLifecycle.StateChanged -= OnDocumentLifecycleStateChanged;
        _documentLifecycle.Dispose();
        _wizardProfileWorkflow.StateChanged -= OnWizardProfileWorkflowStateChanged;
        _documentTreeWorkflow.TreeChanged -= _wizardProfileWorkflow.RefreshSections;
        _wizardProfileWorkflow.Dispose();
        _documentTreeWorkflow.Dispose();
    }
}

/// <summary>A recent-file item bound to the home-screen list.</summary>
/// <param name="Path">Absolute file path (passed to <c>OpenRecentCommand</c>).</param>
/// <param name="DisplayName">The label shown to the user.</param>
public sealed record RecentFileViewModel(string Path, string DisplayName);
