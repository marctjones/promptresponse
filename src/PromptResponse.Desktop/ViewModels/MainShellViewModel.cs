using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PromptResponse.Core.Models;
using PromptResponse.Core.Validation;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels.Editing;
using PromptResponse.Desktop.ViewModels.Prompts;

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
    private readonly EditHistory _editHistory;
    private readonly DataTypeValidator _dataTypeValidator = new();
    private readonly HiddenCharacterAdvisor _hiddenCharAdvisor = new();
    private readonly MixedScriptAdvisor _mixedScriptAdvisor = new();

    private readonly ObservableCollection<PromptViewModelBase> _promptViewModels = new();
    private readonly ObservableCollection<SectionViewModel> _sections = new();
    private readonly ObservableCollection<AdvisoryItem> _advisories = new();

    public MainShellViewModel(
        IFileService fileService,
        IDialogService dialogService,
        IDocumentSessionService session,
        IProfileService profileService,
        PromptViewModelFactory factory,
        EditHistory? editHistory = null)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        _editHistory = editHistory ?? new EditHistory();
        // Reconstruct the factory locally so it threads the shell's edit history
        // into every prompt VM it creates. The injected factory's profile is the
        // same DI singleton — only its history binding differs.
        _factory = new PromptViewModelFactory(profileService, _editHistory);

        Progress = new FormProgressViewModel();
        Search = new SearchViewModel();

        _session.DocumentChanged += OnDocumentChanged;
        _session.DirtyChanged += OnDirtyChanged;
        _profileService.ProfileChanged += (_, _) => OnAllProfileBrushesChanged();
        _editHistory.PropertyChanged += OnEditHistoryChanged;
    }

    /// <summary>The shell-owned undo/redo history. Cleared on document load so
    /// edits in one document never resurface in another.</summary>
    public EditHistory EditHistory => _editHistory;

    public bool CanUndo => _editHistory.CanUndo;
    public bool CanRedo => _editHistory.CanRedo;
    public string UndoLabel => _editHistory.UndoDescription is { } d ? $"Undo {d}" : "Undo";
    public string RedoLabel => _editHistory.RedoDescription is { } d ? $"Redo {d}" : "Redo";

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
            Version = "1.0",
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
            Prompts = new List<Prompt>(),
        };
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
    }

    private void WalkAndDetachPrompts(SectionViewModel s)
    {
        foreach (var p in s.PromptViewModels) DetachDynamicPromptVm(p);
        foreach (var child in s.NestedSections) WalkAndDetachPrompts(child);
    }

    [RelayCommand]
    public async Task Open()
    {
        var doc = await _fileService.OpenFileAsync();
        if (doc == null) return;
        _session.Set(doc, _fileService.CurrentFilePath, dirty: false);
    }

    /// <summary>
    /// Loads a document directly from disk without showing the file picker. Used by
    /// the command-line "--open path" flag and demo flows.
    /// </summary>
    public async Task OpenFromPath(string filePath)
    {
        var doc = await _fileService.LoadFileAsync(filePath);
        if (doc == null) return;
        _session.Set(doc, filePath, dirty: false);
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
        }

        // Templates default to edit mode (the user is authoring); filled forms
        // are never in edit mode. The user can toggle templates to preview-fill
        // via the View menu.
        IsEditMode = _session.Mode == DocumentMode.EditingTemplate;

        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(IsEmptyState));
        OnPropertyChanged(nameof(Mode));
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
        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
        CloseCommand.NotifyCanExecuteChanged();
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
    private void OnPromptResponseChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PromptViewModelBase.Response)) return;
        Progress.Refresh();
        RefreshAdvisories();
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
