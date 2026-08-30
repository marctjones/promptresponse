using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptResponse.Core.Expressions;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using PromptResponse.Core.Validation;
using PromptResponse.Core.Serialization;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels.Editing;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.ViewModels.Signing;
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
    private readonly IAprSerializer _serializer;
    private readonly IMailHandoffService _mailHandoff;
    private readonly IHttpsSubmissionService _httpsSubmission;
    private readonly DocumentOutputWorkflow _outputWorkflow;
    private readonly DocumentSessionWorkflow _sessionWorkflow;
    private readonly EditHistory _editHistory;
    private readonly DataTypeValidator _dataTypeValidator = new();
    private readonly HiddenCharacterAdvisor _hiddenCharAdvisor = new();
    private readonly MixedScriptAdvisor _mixedScriptAdvisor = new();

    private readonly ObservableCollection<PromptViewModelBase> _promptViewModels = new();
    private readonly ObservableCollection<SectionViewModel> _sections = new();
    private readonly ObservableCollection<AdvisoryItem> _advisories = new();
    private readonly SignatureWorkflow _signatureWorkflow;

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
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        _editHistory = editHistory ?? new EditHistory();
        _recentFiles = recentFiles ?? new RecentFilesService();
        // Reconstruct the factory locally so it threads the shell's edit history
        // into every prompt VM it creates. The injected factory's profile is the
        // same DI singleton — only its history binding differs.
        _factory = new PromptViewModelFactory(profileService, _editHistory);
        _serializer = serializer ?? new AprJsonSerializer();
        _mailHandoff = mailHandoff ?? new MailHandoffService();
        _httpsSubmission = httpsSubmission ?? new HttpsSubmissionService();
        _outputWorkflow = new DocumentOutputWorkflow(_session, _fileService, _dialogService);
        _sessionWorkflow = new DocumentSessionWorkflow(_session, _fileService, _dialogService, AddToRecent);
        _signatureWorkflow = new SignatureWorkflow(_session, _fileService, _dialogService, () => _promptViewModels);
        _signatureWorkflow.StateChanged += OnSignatureWorkflowStateChanged;

        Progress = new FormProgressViewModel();
        Search = new SearchViewModel();

        _session.DocumentChanged += OnDocumentChanged;
        _session.DirtyChanged += OnDirtyChanged;
        _profileService.ProfileChanged += (_, _) => OnAllProfileBrushesChanged();
        _editHistory.PropertyChanged += OnEditHistoryChanged;
        _recentFiles.Changed += (_, _) => RefreshRecentFiles();
        RefreshRecentFiles();

        foreach (var t in (templateCatalog?.Templates ?? Array.Empty<StarterTemplate>()))
        {
            StarterTemplates.Add(new RecentFileViewModel(t.Path, t.Title));
        }
    }

    private readonly IRecentFilesService _recentFiles;

    /// <summary>Bundled starter templates shown on the home screen.</summary>
    public ObservableCollection<RecentFileViewModel> StarterTemplates { get; } = new();

    /// <summary>True when there is at least one starter template to offer.</summary>
    public bool HasStarterTemplates => StarterTemplates.Count > 0;

    /// <summary>Starts a new, unsaved document from a bundled starter template.</summary>
    [RelayCommand]
    public async Task NewFromTemplate(string? path)
    {
        await _sessionWorkflow.NewFromTemplateAsync(path);
    }

    /// <summary>Recently opened/saved files shown on the home screen, most-recent-first.</summary>
    public ObservableCollection<RecentFileViewModel> RecentFiles { get; } = new();

    /// <summary>True when there is at least one recent file to offer on the home screen.</summary>
    public bool HasRecentFiles => RecentFiles.Count > 0;

    /// <summary>
    /// First-run onboarding hint: shown on the home screen until the user has
    /// opened or saved something (i.e. while there are no recent files).
    /// </summary>
    public bool ShowGettingStarted => !HasRecentFiles;

    private void RefreshRecentFiles()
    {
        RecentFiles.Clear();
        foreach (var entry in _recentFiles.Items)
        {
            RecentFiles.Add(new RecentFileViewModel(entry.Path, entry.Title));
        }
        OnPropertyChanged(nameof(HasRecentFiles));
        OnPropertyChanged(nameof(ShowGettingStarted));
    }

    private void AddToRecent(string? path, string? title) => _recentFiles.Add(path, title);

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
    public bool IsWizardMode => _profileService.IsActive(typeof(Profiles.WizardModeProfile));

    /// <summary>Active section index in wizard mode. Reset to 0 on every
    /// document load so a fresh document starts at section 1.</summary>
    [ObservableProperty]
    private int _wizardSectionIndex;

    /// <summary>The currently-visible section in wizard mode, or null if no
    /// document is open / no sections exist. Bound by the wizard content area.</summary>
    public SectionViewModel? WizardCurrentSection =>
        WizardSectionIndex >= 0 && WizardSectionIndex < _sections.Count
            ? _sections[WizardSectionIndex]
            : null;

    /// <summary>"Section 3 of 12: Employment History" header for the wizard
    /// nav bar. Empty string when no document is open.</summary>
    public string WizardStepLabel
    {
        get
        {
            if (_sections.Count == 0) return string.Empty;
            var idx = Math.Clamp(WizardSectionIndex, 0, _sections.Count - 1);
            var section = _sections[idx];
            var title = string.IsNullOrWhiteSpace(section.Title) ? "(untitled)" : section.Title;
            return $"Section {idx + 1} of {_sections.Count}: {title}";
        }
    }

    public bool CanWizardPrevious => WizardSectionIndex > 0;
    public bool CanWizardNext => WizardSectionIndex < _sections.Count - 1;

    /// <summary>True when the full-list edit-mode view should render — edit
    /// mode AND wizard mode off.</summary>
    public bool ShowFullEditList => IsEditMode && !IsWizardMode;

    /// <summary>True when the full-list fill-mode view should render — fill
    /// mode AND wizard mode off.</summary>
    public bool ShowFullFillList => !IsEditMode && !IsWizardMode;

    [RelayCommand(CanExecute = nameof(CanWizardPrevious))]
    private void WizardPrevious() => MoveWizard(WizardSectionIndex - 1);

    [RelayCommand(CanExecute = nameof(CanWizardNext))]
    private void WizardNext() => MoveWizard(WizardSectionIndex + 1);

    /// <summary>Jump directly to a specific section index (used by sidebar +
    /// programmatic moves). Out-of-range values are clamped.</summary>
    public void JumpToWizardSection(int index)
    {
        var clamped = Math.Clamp(index, 0, Math.Max(0, _sections.Count - 1));
        if (clamped == WizardSectionIndex) return;
        WizardSectionIndex = clamped;
        // OnWizardSectionIndexChanged fires the dependent-property notifications.
    }

    /// <summary>Toggle wizard mode via the View menu. Flips the underlying
    /// WizardModeProfile flag so the change is observable everywhere (DisplayPreferences,
    /// active-flag summary, persistence).</summary>
    [RelayCommand]
    private void ToggleWizardMode()
    {
        if (_profileService.IsActive(typeof(Profiles.WizardModeProfile)))
        {
            _profileService.Disable<Profiles.WizardModeProfile>();
        }
        else
        {
            _profileService.Enable<Profiles.WizardModeProfile>();
        }
    }

    private void MoveWizard(int toIndex)
    {
        var clamped = Math.Clamp(toIndex, 0, Math.Max(0, _sections.Count - 1));
        if (clamped == WizardSectionIndex) return;
        WizardSectionIndex = clamped;
    }

    partial void OnWizardSectionIndexChanged(int value)
    {
        OnPropertyChanged(nameof(WizardCurrentSection));
        OnPropertyChanged(nameof(WizardStepLabel));
        OnPropertyChanged(nameof(CanWizardPrevious));
        OnPropertyChanged(nameof(CanWizardNext));
        WizardPreviousCommand.NotifyCanExecuteChanged();
        WizardNextCommand.NotifyCanExecuteChanged();
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
        if (string.IsNullOrWhiteSpace(presetName)) return;
        if (Enum.TryParse<ProfilePresets.Preset>(presetName, out var preset))
        {
            ProfilePresets.Apply(preset, _profileService);
        }
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
    public IRenderingProfile ActiveProfile => _profileService.ActiveProfile;

    /// <summary>Profile service exposed for the Display Preferences bindings.</summary>
    public IProfileService ProfileService => _profileService;

    /// <summary>The file service, for dialogs the view owns that need a save picker.</summary>
    public IFileService FileServiceForDialogs => _fileService;

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

    /// <summary>Hairline between regions. Quieter than a component outline.</summary>
    public IBrush ActiveProfileDividerBrush => BrushFor(ColorRole.Divider);

    // ── Shape and density tokens ──
    //
    // Radii lived inline in the XAML and had drifted to six different values, the largest
    // of them 12. Large radii read as consumer software; desktop applications people work
    // in all day sit at two to four. Named here so the answer is in one place and the
    // views stop each choosing their own.

    /// <summary>Corner radius for inputs, buttons and other controls.</summary>
    public CornerRadius ControlCornerRadius { get; } = new(3);

    /// <summary>Corner radius for cards and grouped regions.</summary>
    public CornerRadius SurfaceCornerRadius { get; } = new(4);
    /// <summary>Focus-indicator brush (3px ring under HighContrast, 2px otherwise).</summary>
    public IBrush ActiveProfileFocusBrush => BrushFor(ColorRole.Focus);

    /// <summary>
    /// FluentTheme variant matching the active profile's color scheme. Bound to
    /// Window.RequestedThemeVariant so child controls (RadioButton, CheckBox,
    /// MenuItem) inherit FluentTheme colors that contrast with our profile palette.
    /// </summary>
    public ThemeVariant ActiveThemeVariant => ActiveProfile.ColorScheme switch
    {
        ColorScheme.Dark => ThemeVariant.Dark,
        ColorScheme.HighContrast => ThemeVariant.Dark, // FluentTheme has no HighContrast; Dark is the closer base
        _ => ThemeVariant.Light,
    };

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
    public bool IsEditingTemplate => _session.Mode == DocumentMode.EditingTemplate;
    public bool IsEmptyState => !HasDocument;
    public DocumentMode Mode => _session.Mode;

    /// <summary>The mode, in words rather than as an enum member name.</summary>
    /// <remarks>
    /// The sidebar bound DocumentMode directly and displayed "EditingTemplate" to the
    /// user — an identifier from the source leaking into the interface. Only visible by
    /// looking at a rendered frame.
    /// </remarks>
    public string ModeDescription => _session.Mode switch
    {
        DocumentMode.EditingTemplate => "Editing template",
        DocumentMode.FillingForm => "Filling in",
        _ => string.Empty,
    };
    public string Title => _session.Title;

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
    public string CurrentDocumentTitle => _session.CurrentDocument?.Metadata.Title ?? string.Empty;
    public string? DocumentDescription => _session.CurrentDocument?.Metadata.Description;
    public bool HasDocumentDescription => !string.IsNullOrWhiteSpace(DocumentDescription);

    /// <summary>Editable wrapper around the active document's metadata. Bound by
    /// the edit-mode metadata panel; null when no document is open. When the user
    /// types in any metadata field, this VM raises Changed so the shell marks
    /// the document dirty and refreshes derived display properties.</summary>
    public DocumentMetadataViewModel? Metadata { get; private set; }

    /// <summary>Count of advisory warnings from the data-type validator (never errors — vision invariant).</summary>
    public int AdvisoryCount => _advisories.Count;
    public bool HasAdvisories => _advisories.Count > 0;
    public string AdvisorySummary => _advisories.Count switch
    {
        0 => "No advisories",
        1 => "1 advisory",
        _ => $"{_advisories.Count} advisories",
    };

    /// <summary>Itemized list of advisories. Each entry links back to the prompt that
    /// triggered it (PromptId, PromptLabel) and explains why (Message). Pinned in the
    /// right rail so the user can see which fields need attention without hunting.</summary>
    public IReadOnlyList<AdvisoryItem> Advisories => _advisories;

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

    // ── signatures (verify / trust status) ───────────────────────────────────

    /// <summary>The document's signatures with their verification + trust status.</summary>
    public IReadOnlyList<SignatureStatusItem> Signatures => _signatureWorkflow.Signatures;

    /// <summary>Whether the open document carries any signatures.</summary>
    public bool HasSignatures => _signatureWorkflow.HasSignatures;

    /// <summary>A one-line summary for the signatures panel.</summary>
    public string SignatureSummary => _signatureWorkflow.SignatureSummary;

    /// <summary>
    /// Re-verifies the document's signatures (default trust: certificates are
    /// reported as trusted only if self-signed certs are pinned or CA certs chain
    /// to a configured root — neither is wired in the GUI yet, so self-signed certs
    /// show as "SelfSigned"). Cheap to call on load; not run on every keystroke.
    /// </summary>
    public void RefreshSignatures()
    {
        _signatureWorkflow.Refresh();
    }

    private void OnSignatureWorkflowStateChanged()
    {
        OnPropertyChanged(nameof(Signatures));
        OnPropertyChanged(nameof(HasSignatures));
        OnPropertyChanged(nameof(SignatureSummary));
        OnPropertyChanged(nameof(SignatureBreakageNotice));
        OnPropertyChanged(nameof(HasSignatureBreakageNotice));
    }

    // ── Telling somebody when their edit has broken a signature ──────────────

    /// <summary>What to say when an edit has just invalidated somebody's signature.</summary>
    /// <remarks>
    /// <para>
    /// Said at the moment it happens, and never by preventing the edit. Any string is a
    /// valid response and a person may need to correct a signed field — the format's
    /// answer to that is not to stop them, it is to make sure they know what it cost.
    /// </para>
    /// <para>
    /// A signature is <em>never</em> removed to tidy this up. Doing so would turn
    /// "somebody signed this and it was altered" into "nobody ever signed this", which is
    /// strictly less informative and would make this editor the easiest tampering tool
    /// available. Removing one is a separate, deliberate command.
    /// </para>
    /// <para>
    /// Announced once per signature. A message that reappears on every keystroke is a
    /// message people learn to dismiss without reading.
    /// </para>
    /// </remarks>
    public string? SignatureBreakageNotice
    {
        get => _signatureWorkflow.BreakageNotice;
    }

    /// <summary>Whether there is a breakage to tell somebody about.</summary>
    public bool HasSignatureBreakageNotice => _signatureWorkflow.HasBreakageNotice;

    /// <summary>Removes one signature, on purpose and after asking.</summary>
    /// <remarks>
    /// <para>
    /// The only way a signature leaves a document in this application. Editing a signed
    /// field never removes one, however broken it becomes: a broken signature is evidence
    /// that somebody signed and the document changed afterwards, and discarding it would
    /// convert that into "nobody ever signed this" — strictly less informative, and it
    /// would make this editor the easiest tampering tool anyone could reach for.
    /// </para>
    /// <para>
    /// So removal exists, because it is the person's own file and they can edit the JSON
    /// regardless, but it is a decision they make rather than a convenience they trip
    /// over.
    /// </para>
    /// </remarks>
    [RelayCommand]
    private async Task RemoveSignature(string? signatureId)
    {
        await _signatureWorkflow.RemoveAsync(signatureId);
    }

    /// <summary>Dismisses the notice without undoing anything.</summary>
    [RelayCommand]
    private void DismissSignatureNotice() => _signatureWorkflow.DismissBreakageNotice();

    /// <summary>Undoes the edit that broke it, and clears the notice.</summary>
    /// <remarks>
    /// Offered because the usual reason to break a signature is not meaning to. The edit
    /// is ordinary undo history, so this is the same action the Edit menu already has —
    /// put next to the message so it is reachable when it is relevant.
    /// </remarks>
    [RelayCommand]
    private void RestoreSignedValues()
    {
        _signatureWorkflow.RestoreSignedValues();
    }

    /// <summary>Re-verifies signatures on demand (e.g. after editing responses).</summary>
    [RelayCommand]
    public void RefreshSignaturesNow() => RefreshSignatures();

    /// <summary>
    /// Signs the open document as the publisher with a chosen X.509 certificate
    /// (.pfx), binding a submission URL. Adds the signature and marks the document
    /// dirty so the user saves it.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    public async Task SignAsPublisher()
    {
        await _signatureWorkflow.SignAsPublisherAsync();
    }

    /// <summary>
    /// Signs the responses the user has filled in (all answered fields) with a
    /// chosen X.509 certificate.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    public async Task SignMyResponses()
    {
        await _signatureWorkflow.SignMyResponsesAsync();
    }

    /// <summary>
    /// Re-runs the advisory inspection over the current document. Per the vision,
    /// these are never blocking — they're hints surfaced for the user's awareness
    /// (e.g., "five" in a number-hinted field renders an advisory but is still valid).
    /// </summary>
    public void RefreshAdvisories()
    {
        _advisories.Clear();
        var doc = _session.CurrentDocument;
        if (doc != null)
        {
            // Data-type hint mismatches ("five" in a number-hinted field, etc.)
            var typeResult = _dataTypeValidator.ValidateDocument(doc);
            foreach (var warning in typeResult.Warnings)
            {
                var (promptId, promptLabel) = ResolvePromptFromPath(doc, warning.PropertyPath);
                _advisories.Add(new AdvisoryItem(promptId, promptLabel, warning.Message));
            }
            // Hidden-character findings (ZWSP, soft hyphen, bidi marks, variation
            // selectors) — preserved on save but flagged so the user can confirm intent.
            var hiddenResult = _hiddenCharAdvisor.Validate(doc);
            foreach (var warning in hiddenResult.Warnings)
            {
                var (promptId, promptLabel) = ResolvePromptFromPath(doc, warning.PropertyPath);
                _advisories.Add(new AdvisoryItem(promptId, promptLabel, warning.Message));
            }
            // Mixed-script findings on URL hosts and email domains (homoglyph attack
            // vector — Cyrillic 'а' in аpple.com).
            var mixedResult = _mixedScriptAdvisor.Validate(doc);
            foreach (var warning in mixedResult.Warnings)
            {
                var (promptId, promptLabel) = ResolvePromptFromPath(doc, warning.PropertyPath);
                _advisories.Add(new AdvisoryItem(promptId, promptLabel, warning.Message));
            }

            // Cross-field validation (exprValidation) — advisory, never blocking.
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var expressions = FormExpressions.BuildContext(doc, today);
            foreach (var prompt in FormExpressions.GetAllPrompts(doc))
            {
                var message = FormExpressions.Validate(prompt, expressions);
                if (message != null)
                {
                    _advisories.Add(new AdvisoryItem(prompt.Id, prompt.Label, message));
                }
            }
        }
        OnPropertyChanged(nameof(AdvisoryCount));
        OnPropertyChanged(nameof(HasAdvisories));
        OnPropertyChanged(nameof(AdvisorySummary));
        OnPropertyChanged(nameof(Advisories));
    }

    /// <summary>
    /// Resolves a validator's PropertyPath (which is the prompt's Id) to the
    /// prompt's user-visible label. Falls back to the id when the prompt can't
    /// be located so the advisory remains informative.
    /// </summary>
    private static (string id, string label) ResolvePromptFromPath(AprDocument doc, string propertyPath)
    {
        var prompt = FindPromptById(doc.Sections, propertyPath);
        return prompt != null ? (prompt.Id, prompt.Label) : (propertyPath, propertyPath);
    }

    private static Prompt? FindPromptById(IList<Section> sections, string id)
    {
        foreach (var section in sections)
        {
            foreach (var p in section.Prompts) if (p.Id == id) return p;
            var nested = FindPromptById(section.Sections, id);
            if (nested != null) return nested;
        }
        return null;
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
    {
        var doc = _session.CurrentDocument;
        if (doc == null) return;
        var section = new Section
        {
            Id = $"section_{Guid.NewGuid():N}",
            Title = "New section",
        };
            // A section must carry content (specification 4.3), so a new one arrives with
            // a starter prompt rather than as an empty shell that makes the document
            // invalid the moment it is added. The author renames it; they never have to
            // repair it.
        section.Prompts.Add(new Prompt { Id = $"{section.Id}.prompt_1", Label = "New prompt" });
        var vm = new SectionViewModel(section, _factory, depth: 0,
            onPromptAdded: AttachDynamicPromptVm,
            onPromptRemoved: DetachDynamicPromptVm,
            history: _editHistory);
        var index = _sections.Count;

        if (!_editHistory.IsApplying)
        {
            _editHistory.Execute(new AddTopLevelSectionCommand(this, section, vm, index));
        }
        else
        {
            ApplyAddTopLevelSectionAt(index, section, vm);
        }
    }

    /// <summary>Remove a top-level section from the current document. The user
    /// triggers this via the editor's section-list ✕ button. Disposes any prompt
    /// VMs under the removed subtree so subscriptions and resources are released.</summary>
    [RelayCommand]
    public void RemoveTopLevelSection(SectionViewModel? sectionVm)
    {
        if (sectionVm is null) return;
        var doc = _session.CurrentDocument;
        if (doc == null) return;
        if (!_sections.Contains(sectionVm)) return;

        if (!_editHistory.IsApplying)
        {
            var index = _sections.IndexOf(sectionVm);
            _editHistory.Execute(new RemoveTopLevelSectionCommand(this, sectionVm, index));
        }
        else
        {
            ApplyRemoveTopLevelSection(sectionVm);
        }
    }

    /// <summary>Reorder a top-level section. Undoable.</summary>
    public void MoveTopLevelSection(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;
        var doc = _session.CurrentDocument;
        if (doc == null) return;
        if (fromIndex < 0 || fromIndex >= _sections.Count) return;
        if (toIndex < 0 || toIndex >= _sections.Count) return;

        if (!_editHistory.IsApplying)
            _editHistory.Execute(new MoveTopLevelSectionCommand(this, fromIndex, toIndex));
        else
            ApplyMoveTopLevelSection(fromIndex, toIndex);
    }

    internal void ApplyMoveTopLevelSection(int fromIndex, int toIndex)
    {
        var doc = _session.CurrentDocument;
        if (doc == null) return;
        var sec = doc.Sections[fromIndex];
        doc.Sections.RemoveAt(fromIndex);
        doc.Sections.Insert(toIndex, sec);
        var vm = _sections[fromIndex];
        _sections.RemoveAt(fromIndex);
        _sections.Insert(toIndex, vm);
        _session.MarkDirty();
    }

    /// <summary>Raw mutation: insert a top-level section at the given index. Used
    /// by both the public AddTopLevelSection and undo of RemoveTopLevelSection.</summary>
    internal void ApplyAddTopLevelSectionAt(int index, Section section, SectionViewModel vm)
    {
        var doc = _session.CurrentDocument;
        if (doc == null) return;
        if (index < 0 || index > doc.Sections.Count) index = doc.Sections.Count;
        doc.Sections.Insert(index, section);
        if (index > _sections.Count) index = _sections.Count;
        _sections.Insert(index, vm);
        _session.MarkDirty();
        RefreshWizardDerived();
    }

    /// <summary>Raw mutation: remove a top-level section. Used by both the
    /// public RemoveTopLevelSection and undo of AddTopLevelSection.</summary>
    internal void ApplyRemoveTopLevelSection(SectionViewModel sectionVm)
    {
        var doc = _session.CurrentDocument;
        if (doc == null) return;
        if (!_sections.Contains(sectionVm)) return;
        WalkAndDetachPrompts(sectionVm);
        doc.Sections.Remove(sectionVm.Model);
        _sections.Remove(sectionVm);
        _session.MarkDirty();
        // Clamp wizard index in case it pointed past the last remaining section.
        if (WizardSectionIndex >= _sections.Count && _sections.Count > 0)
        {
            WizardSectionIndex = _sections.Count - 1;
        }
        RefreshWizardDerived();
    }

    private void RefreshWizardDerived()
    {
        OnPropertyChanged(nameof(WizardCurrentSection));
        OnPropertyChanged(nameof(WizardStepLabel));
        OnPropertyChanged(nameof(CanWizardPrevious));
        OnPropertyChanged(nameof(CanWizardNext));
        WizardPreviousCommand.NotifyCanExecuteChanged();
        WizardNextCommand.NotifyCanExecuteChanged();
    }

    private void WalkAndDetachPrompts(SectionViewModel s)
    {
        foreach (var p in s.PromptViewModels) DetachDynamicPromptVm(p);
        foreach (var child in s.NestedSections) WalkAndDetachPrompts(child);
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
    public bool CanSubmitViaEmail() => HasDocument && !IsEditMode &&
        _session.CurrentDocument?.Metadata.SubmissionUrls?.Any(url => MailHandoffService.TryGetRecipient(url, out _)) == true;

    [RelayCommand(CanExecute = nameof(CanSubmitViaEmail))]
    public async Task SubmitViaEmail()
    {
        var source = _session.CurrentDocument;
        if (source?.Metadata.SubmissionUrls is not { Count: > 0 } targets) return;

        var choices = targets
            .Where(url => MailHandoffService.TryGetRecipient(url, out _))
            .ToList();
        if (choices.Count == 0) return;

        var selectedIndex = await _dialogService.ShowChoiceAsync(
            "Submit via email",
            "Choose the email destination. This opens a draft only; you review and send it yourself.",
            choices);
        if (selectedIndex is null) return;

        var outputPath = await _fileService.PickExportPathAsync(
            SuggestedSubmissionFileName(source.Metadata.Title), "Save completed APR file", "APR Filled Form", "aprf");
        if (string.IsNullOrWhiteSpace(outputPath)) return;

        if (!await _dialogService.ShowConfirmationAsync(
                "Open email draft",
                $"A completed APR copy will be saved as {Path.GetFileName(outputPath)} and an email draft addressed to {choices[selectedIndex.Value]} will be opened. PromptResponse will not send the email. Continue?")) return;

        // Clone through the format serializer. The open template stays a template; the
        // outgoing artifact is explicitly a filled form and has its own save location.
        var completedCopy = _serializer.Deserialize(_serializer.Serialize(source));
        completedCopy.DocumentType = DocumentType.FilledForm;
        var previousPath = _fileService.CurrentFilePath;
        await _fileService.SaveFileAsync(completedCopy, outputPath);
        if (string.IsNullOrEmpty(previousPath)) _fileService.ClearCurrentFilePath();
        else _fileService.SetCurrentFilePath(previousPath);

        var result = await _mailHandoff.ComposeAsync(new MailHandoffRequest(
            choices[selectedIndex.Value], outputPath,
            $"Completed APR form: {source.Metadata.Title ?? "Untitled form"}",
            "Please find the completed APR form attached."));
        AddToRecent(outputPath, completedCopy.Metadata.Title);
        await _dialogService.ShowConfirmationAsync("Email handoff", result.Message + Environment.NewLine + outputPath);
    }

    private static string SuggestedSubmissionFileName(string? title) =>
        string.IsNullOrWhiteSpace(title) ? "completed-form.aprf" : $"{title}-completed.aprf";

    public bool CanSubmitViaHttps() => HasDocument && !IsEditMode && _session.CurrentDocument?.Metadata.SubmissionUrls?.Any(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps) == true;

    [RelayCommand(CanExecute = nameof(CanSubmitViaHttps))]
    public async Task SubmitViaHttps()
    {
        var source = _session.CurrentDocument;
        var targets = source?.Metadata.SubmissionUrls?.Where(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps).ToList() ?? [];
        if (targets.Count == 0) return;
        var selected = await _dialogService.ShowChoiceAsync("Submit via HTTPS", "Choose one destination. PromptResponse will POST only after you confirm; it never follows redirects or falls back.", targets);
        if (selected is null || !await _dialogService.ShowConfirmationAsync("Submit completed APR", $"POST this completed APR to {targets[selected.Value]}?")) return;
        var completed = _serializer.Deserialize(_serializer.Serialize(source!));
        completed.DocumentType = DocumentType.FilledForm;
        var result = await _httpsSubmission.SubmitAsync(targets[selected.Value], _serializer.Serialize(completed));
        await _dialogService.ShowConfirmationAsync("HTTPS submission", result.Message);
    }

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
        Progress.SetDocument(document);
        Search.SetDocument(document);

        // Dispose the previous prompt VMs (also unsubscribes our PropertyChanged
        // handler) and rebuild the section tree.
        foreach (var vm in _promptViewModels)
        {
            vm.PropertyChanged -= OnPromptResponseChanged;
            vm.Dispose();
        }
        _promptViewModels.Clear();
        _sections.Clear();
        if (Metadata != null)
        {
            Metadata.Changed -= OnMetadataChanged;
        }
        Metadata = null;

        // Drop any undo history from the previous document — edits in one
        // document must never resurface as undo steps in another.
        _editHistory.Clear();

        if (document != null)
        {
            Metadata = new DocumentMetadataViewModel(document.Metadata, _editHistory);
            Metadata.Changed += OnMetadataChanged;

            foreach (var section in document.Sections)
            {
                // onPromptAdded/onPromptRemoved fire when dynamic table rows are
                // added or removed at runtime — keep the shell-tracked prompt VM
                // list in sync so progress + advisories pick up the new cells.
                var sectionVm = new SectionViewModel(
                    section, _factory, depth: 0,
                    onPromptAdded: AttachDynamicPromptVm,
                    onPromptRemoved: DetachDynamicPromptVm,
                    history: _editHistory);
                _sections.Add(sectionVm);
                CollectPrompts(sectionVm);
            }
            // Subscribe to every prompt's Response changes so the progress bar and
            // advisory list refresh as the user types — fixes the "progress never
            // updates / advisories require Refresh button" live-app bugs.
            foreach (var promptVm in _promptViewModels)
            {
                promptVm.PropertyChanged += OnPromptResponseChanged;
            }

            // Resolve who each field is for, so the person filling never has to ask.
            ApplyRoles(document);

            // Apply expression hints once up-front so initial visibility,
            // read-only, and computed values are correct before any edit.
            ApplyExpressions();
        }

        // Templates default to edit mode (the user is authoring); filled forms
        // are never in edit mode. The user can toggle templates to preview-fill
        // via the View menu.
        IsEditMode = _session.Mode == DocumentMode.EditingTemplate;

        // Reset wizard navigation to the first section on document load so a
        // fresh document doesn't open mid-wizard from a previous document.
        WizardSectionIndex = 0;
        OnPropertyChanged(nameof(WizardCurrentSection));
        OnPropertyChanged(nameof(WizardStepLabel));
        OnPropertyChanged(nameof(CanWizardPrevious));
        OnPropertyChanged(nameof(CanWizardNext));
        WizardPreviousCommand.NotifyCanExecuteChanged();
        WizardNextCommand.NotifyCanExecuteChanged();

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
        RefreshSignatures();
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
        CloseCommand.NotifyCanExecuteChanged();
    }

    // ── Roles (specification 4.10) ──

    private readonly ObservableCollection<RoleChoice> _availableRoles = new();
    private RoleChoice? _activeRoleChoice;

    /// <summary>The roles this form uses, plus "everyone" for showing the whole thing.</summary>
    /// <remarks>
    /// Empty for a single-party form, which is most of them, so the picker never appears
    /// where it would only be clutter.
    /// </remarks>
    public ObservableCollection<RoleChoice> AvailableRoles => _availableRoles;

    /// <summary>Whether this form is filled by more than one party.</summary>
    public bool HasRoles => _availableRoles.Count > 1;

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
        get => _activeRoleChoice;
        set
        {
            if (_activeRoleChoice == value) return;
            _activeRoleChoice = value;
            foreach (var promptVm in _promptViewModels)
            {
                promptVm.ActiveRole = value?.Id;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActiveRoleSummary));
            OnPropertyChanged(nameof(ActiveRoleDescription));
        }
    }

    /// <summary>The author's sentence about the selected role, when they wrote one.</summary>
    public string? ActiveRoleDescription => ActiveRoleChoice?.Description;

    /// <summary>What the shell says about the current selection.</summary>
    public string ActiveRoleSummary
    {
        get
        {
            if (!HasRoles) return string.Empty;
            if (ActiveRoleChoice?.Id is null)
            {
                return "Showing every part of this form.";
            }

            var mine = _promptViewModels.Count(p => p.IsMine);
            return $"{mine} of {_promptViewModels.Count} fields are for " +
                   $"{ActiveRoleChoice.Name}. The rest are marked, and still answerable.";
        }
    }

    private void ApplyRoles(AprDocument document)
    {
        // Tolerant of duplicate ids: a document with two prompts sharing an id is
        // invalid, and it must still open so the editor can show the person what is
        // wrong (specification 6.3). ToDictionary threw here, which turned a fixable
        // document into an unopenable one.
        var resolved = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (prompt, role) in FormRoles.Resolve(document))
        {
            resolved.TryAdd(prompt.Id, role);
        }

        foreach (var promptVm in _promptViewModels)
        {
            var role = resolved.GetValueOrDefault(promptVm.Model.Id);
            promptVm.Role = role;
            promptVm.RoleDisplayName = FormRoles.DisplayName(document, role);
        }

        _availableRoles.Clear();
        var used = FormRoles.Used(document);
        if (used.Count > 0)
        {
            // "Everyone" first, and selected, so a form opens looking exactly as it did
            // before roles existed until someone says who they are.
            _availableRoles.Add(new RoleChoice(null, "Everyone", "Show every part of this form"));
            foreach (var id in used)
            {
                var definition = FormRoles.Definition(document, id);
                _availableRoles.Add(new RoleChoice(id, definition?.DisplayName ?? id, definition?.Description));
            }
        }

        _activeRoleChoice = _availableRoles.FirstOrDefault();
        foreach (var promptVm in _promptViewModels)
        {
            promptVm.ActiveRole = _activeRoleChoice?.Id;
        }

        OnPropertyChanged(nameof(HasRoles));
        OnPropertyChanged(nameof(ActiveRoleChoice));
        OnPropertyChanged(nameof(ActiveRoleSummary));
    }

    private void CollectPrompts(SectionViewModel section)
    {
        foreach (var prompt in section.PromptViewModels) _promptViewModels.Add(prompt);
        foreach (var nested in section.NestedSections) CollectPrompts(nested);
    }

    /// <summary>Mark dirty + refresh derived display properties whenever the user
    /// edits any metadata field via the edit-mode metadata panel.</summary>
    private void OnMetadataChanged(object? sender, EventArgs e)
    {
        _session.MarkDirty();
        OnPropertyChanged(nameof(CurrentDocumentTitle));
        OnPropertyChanged(nameof(DocumentDescription));
        OnPropertyChanged(nameof(HasDocumentDescription));
        OnPropertyChanged(nameof(Title));
    }

    /// <summary>Wire a newly-added dynamic cell prompt (from AddRow) into the shell's
    /// tracked list so its Response changes drive progress + advisory refresh.</summary>
    private void AttachDynamicPromptVm(PromptViewModelBase promptVm)
    {
        _promptViewModels.Add(promptVm);
        promptVm.PropertyChanged += OnPromptResponseChanged;
        Progress.Refresh();
        RefreshAdvisories();
    }

    /// <summary>Detach a removed dynamic cell prompt (from RemoveRow), unsubscribe,
    /// and dispose so the rendering profile event handler is released.</summary>
    private void DetachDynamicPromptVm(PromptViewModelBase promptVm)
    {
        promptVm.PropertyChanged -= OnPromptResponseChanged;
        _promptViewModels.Remove(promptVm);
        promptVm.Dispose();
        Progress.Refresh();
        RefreshAdvisories();
    }

    /// <summary>
    /// Pulses progress + advisories whenever any prompt VM's Response changes.
    /// Filtered to Response-only updates so other property pulses (DisplayValue,
    /// Show* derived bools) don't trigger redundant validation passes.
    /// </summary>
    private bool _applyingExpressions;

    private void OnPromptResponseChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PromptViewModelBase.Response)) return;
        if (_applyingExpressions) return;   // ignore the cascade from ApplyExpressions itself
        ApplyExpressions();
        Progress.Refresh();
        RefreshAdvisories();

        // Signatures too. Typing into a signed field is precisely what invalidates a
        // signature, so this is the moment the status changes - and it used to refresh
        // only when somebody pressed a button, which meant a field could keep reporting
        // "signed" after the keystroke that broke it.
        RefreshSignatures();
    }

    /// <summary>
    /// Evaluates the document's expression hints against the current responses:
    /// recomputes computed (<c>exprValue</c>) fields, then sets each prompt VM's
    /// visibility (<c>exprHidden</c>) and read-only (<c>exprValue</c>/<c>exprReadOnly</c>)
    /// state. Re-entrancy-guarded since recompute writes back into the model.
    /// </summary>
    public void ApplyExpressions()
    {
        var doc = _session.CurrentDocument;
        if (doc == null || _applyingExpressions) return;

        _applyingExpressions = true;
        try
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            FormExpressions.RecomputeComputedValues(doc, today);
            // Rebuilt after recomputation so dependent expressions see the new values.
            var expressions = FormExpressions.BuildContext(doc, today);

            var prompts = new Dictionary<string, Prompt>(StringComparer.Ordinal);
            foreach (var p in FormExpressions.GetAllPrompts(doc))
            {
                if (!string.IsNullOrEmpty(p.Id))
                {
                    prompts[p.Id] = p;   // last wins on the rare duplicate id
                }
            }

            foreach (var vm in _promptViewModels)
            {
                if (!prompts.TryGetValue(vm.Id, out var prompt))
                {
                    continue;
                }
                vm.IsVisible = !FormExpressions.IsHidden(prompt, expressions);
                vm.IsReadOnly = FormExpressions.IsReadOnly(prompt, expressions);
                vm.RefreshFromModel();   // pick up any recomputed value
            }
        }
        finally
        {
            _applyingExpressions = false;
        }
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
        _signatureWorkflow.StateChanged -= OnSignatureWorkflowStateChanged;
        foreach (var vm in _promptViewModels)
        {
            vm.Dispose();
        }
        _promptViewModels.Clear();
    }
}

/// <summary>A recent-file item bound to the home-screen list.</summary>
/// <param name="Path">Absolute file path (passed to <c>OpenRecentCommand</c>).</param>
/// <param name="DisplayName">The label shown to the user.</param>
public sealed record RecentFileViewModel(string Path, string DisplayName);
