using System.ComponentModel;
using System.Runtime.CompilerServices;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Editing;
using PromptResponse.Desktop.ViewModels.Prompts.Presentation;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Base class for the polymorphic prompt-rendering ViewModels. Every concrete
/// type-specific VM (Text, Number, Date, Boolean, Select, …) inherits this base.
/// Each VM is small, focused on a single data-type hint, and individually testable.
/// </summary>
/// <remarks>
/// VISION INVARIANTS:
///   * Response is always the raw string the user typed; setting it never enforces
///     a type. "five" in a number-hinted prompt is a valid response.
///   * DisplayValue is computed from Response via the active rendering profile;
///     it can never be the source of truth for the persisted document.
///   * ProfileChanged on the IProfileService raises PropertyChanged on DisplayValue
///     so the bound view re-renders without a manual rebind.
/// </remarks>
public abstract class PromptViewModelBase : INotifyPropertyChanged, IDisposable
{
    private readonly Prompt _prompt;
    private readonly IProfileService _profileService;
    private readonly EditHistory? _history;
    private readonly PromptProfileRefreshCoordinator _profileRefresh;
    private readonly PromptResponseState _responseState;
    private bool _disposed;

    protected PromptViewModelBase(Prompt prompt, IProfileService profileService, EditHistory? history = null)
    {
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _history = history;
        _profileRefresh = new PromptProfileRefreshCoordinator(
            _profileService,
            propertyName => Notify(propertyName),
            OnDerivedPropertiesShouldRefresh);
        _responseState = new PromptResponseState(_prompt, Notify, OnDerivedPropertiesShouldRefresh);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id
    {
        get => _prompt.Id;
        set => SetWithUndo(nameof(Id), () => _prompt.Id, v => _prompt.Id = v, value ?? string.Empty);
    }

    public string Label
    {
        get => _prompt.Label;
        set => SetWithUndo(nameof(Label), () => _prompt.Label, v => _prompt.Label = v, value ?? string.Empty);
    }

    public string? HelpText
    {
        get => _prompt.Hints.HelpText;
        set => SetWithUndo(nameof(HelpText), () => _prompt.Hints.HelpText, v => _prompt.Hints.HelpText = v, value);
    }

    public string? Placeholder
    {
        get => _prompt.Hints.Placeholder;
        set => SetWithUndo(nameof(Placeholder), () => _prompt.Hints.Placeholder, v => _prompt.Hints.Placeholder = v, value);
    }

    public string? ExpectedDataType
    {
        get => _prompt.Hints.ExpectedDataType;
        set => SetWithUndo(
            nameof(ExpectedDataType),
            () => _prompt.Hints.ExpectedDataType,
            v =>
            {
                _prompt.Hints.ExpectedDataType = v;
                _responseState.RefreshDisplayAndDerivedState();
            },
            value);
    }

    /// <summary>Optional regex hint for advisory pattern validation.</summary>
    public string? ValidationPattern
    {
        get => _prompt.Hints.ValidationPattern;
        set => SetWithUndo(nameof(ValidationPattern), () => _prompt.Hints.ValidationPattern, v => _prompt.Hints.ValidationPattern = v, value);
    }

    /// <summary>Routes scalar prompt metadata edits through the shared edit-history
    /// coordinator while preserving this view model as the merge target.</summary>
    private void SetWithUndo<T>(string propertyName, Func<T> getter, Action<T> applySetter, T newValue)
        => PropertyEditCoordinator.Apply(this, propertyName, _history, getter, applySetter, newValue, Notify);

    /// <summary>Underlying model — exposed for the editor surface to access fields
    /// (e.g. SuggestedValues list) directly. Fill-mode rendering should not use this.</summary>
    internal Prompt Model => _prompt;

    // ── Provenance: did the form work this out, or did you? (specification 8.6) ──

    /// <summary>Whether this field's value is derived from an expression.</summary>
    public bool IsComputedField => PromptProvenancePresentation.IsComputed(_prompt);

    /// <summary>Whether the value on screen is the one the expression produced.</summary>
    public bool ValueIsCalculated => PromptProvenancePresentation.IsCalculated(_prompt);

    /// <summary>Whether somebody typed over a value the form had worked out.</summary>
    /// <remarks>
    /// The state that most needs saying. A person who corrects a computed total needs to
    /// know two things they cannot otherwise see: that they have overridden something,
    /// and that recomputation will now leave their answer alone rather than reverting it.
    /// Without a mark for it, the override behaviour the format guarantees is invisible,
    /// and somebody who does not trust it will keep re-checking a number that is not
    /// going to change back.
    /// </remarks>
    public bool ValueWasOverridden => PromptProvenancePresentation.WasOverridden(_prompt);

    /// <summary>Whether to show anything about where this value came from.</summary>
    /// <remarks>
    /// Nothing on an ordinary field. Most fields are ordinary, and marking them all
    /// "typed by you" would say nothing while burying the marks that mean something.
    /// </remarks>
    public bool ShowProvenanceMark => ValueIsCalculated || ValueWasOverridden;

    /// <summary>The text beside the field. Text, never colour alone.</summary>
    public string? ProvenanceLabel =>
        PromptProvenancePresentation.Label(ValueIsCalculated, ValueWasOverridden);

    /// <summary>What assistive technology should say about where this value came from.</summary>
    /// <remarks>
    /// Fuller than the visible mark, and in both states it says the thing the mark cannot
    /// fit: that the field is yours to change, and what happens if you do.
    /// </remarks>
    public string? ProvenanceAnnouncement
    {
        get
        {
            return PromptProvenancePresentation.Announcement(
                ValueIsCalculated, ValueWasOverridden, ActiveProfile.LiveRegions);
        }
    }

    /// <summary>Whether the mark may carry a colour cue as well as its text.</summary>
    public bool ProvenanceColorCue => ShowProvenanceMark && ActiveProfile.ColorCuesEnabled;

    // ── Roles: whose field is this? (specification 4.10) ──

    private string? _activeRole;
    private string? _roleDisplayName;

    /// <summary>The role this field is for, or null when it is for whoever is filling.</summary>
    /// <remarks>
    /// Set by the shell after resolution, because a prompt inherits its section's role and
    /// a prompt view model cannot see its ancestors.
    /// </remarks>
    public string? Role { get; internal set; }

    /// <summary>The role's declared name, or its identifier when the form declares none.</summary>
    public string? RoleDisplayName
    {
        get => _roleDisplayName;
        internal set
        {
            if (_roleDisplayName == value) return;
            _roleDisplayName = value;
            Notify();
            Notify(nameof(RoleBadge));
            Notify(nameof(RoleAnnouncement));
        }
    }

    /// <summary>Which role the person at the keyboard says they are filling.</summary>
    public string? ActiveRole
    {
        get => _activeRole;
        internal set
        {
            if (_activeRole == value) return;
            _activeRole = value;
            Notify();
            Notify(nameof(IsMine));
            Notify(nameof(IsSomeoneElses));
            Notify(nameof(ShowsMineAccent));
            Notify(nameof(RoleBadge));
            Notify(nameof(RoleAnnouncement));
        }
    }

    /// <summary>True when this field is one the active role is meant to fill.</summary>
    /// <remarks>
    /// A field with no role belongs to whoever is filling, so it is always theirs. With no
    /// active role chosen, every field is "mine" - nobody has said otherwise, and the form
    /// should look exactly as it did before roles existed.
    /// </remarks>
    public bool IsMine => PromptRolePresentation.IsMine(Role, ActiveRole);

    /// <summary>True when this field is marked for a different party.</summary>
    /// <remarks>
    /// Marked, never locked. <see cref="IsInputEnabled"/> is deliberately untouched: the
    /// format has no idea who is at the keyboard, and a disabled box is evidence of
    /// nothing since whoever holds the file can edit the JSON directly. The point is to
    /// answer "is this one mine?" without anybody having to ask, not to stop them.
    /// </remarks>
    public bool IsSomeoneElses => !IsMine;

    /// <summary>Whether to draw the "this one is yours" accent.</summary>
    /// <remarks>
    /// Only once somebody has said which role they are filling. Before that every field is
    /// unmarked and the form looks exactly as it did before roles existed, which is right:
    /// accenting everything says nothing.
    /// </remarks>
    public bool ShowsMineAccent => !string.IsNullOrWhiteSpace(ActiveRole) && IsMine;

    /// <summary>A short badge for a field belonging to someone else, else null.</summary>
    public string? RoleBadge => PromptRolePresentation.Badge(IsMine, RoleDisplayName);

    /// <summary>What a screen reader should say about whose field this is.</summary>
    /// <remarks>
    /// A visual treatment communicates nothing to someone using a screen reader, so the
    /// same fact goes into the accessible description. Specification 4.10 asks for this
    /// explicitly; the whole point of a role is that nobody has to ask whether a field is
    /// theirs, and that has to hold for everyone.
    /// </remarks>
    public string? RoleAnnouncement => PromptRolePresentation.Announcement(IsMine, RoleDisplayName);

    /// <summary>
    /// Raw response string. Setting this updates the underlying <see cref="Prompt"/> model.
    /// Any visible text is a valid response (vision invariant).
    /// </summary>
    public string Response
    {
        get => _responseState.Response;
        set => _responseState.Response = value;
    }

    private bool _isVisible = true;

    /// <summary>
    /// Whether this prompt is shown. Driven by the document's <c>exprHidden</c>
    /// hint (conditional visibility); defaults to visible. The view binds the
    /// prompt container's visibility to this.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value) return;
            _isVisible = value;
            Notify(nameof(IsVisible));
        }
    }

    private bool _isReadOnly;

    /// <summary>
    /// Whether this prompt is read-only — true for computed fields (<c>exprValue</c>)
    /// and when <c>exprReadOnly</c> is truthy. The view binds input editability to
    /// <see cref="IsInputEnabled"/>.
    /// </summary>
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set
        {
            if (_isReadOnly == value) return;
            _isReadOnly = value;
            Notify(nameof(IsReadOnly));
            Notify(nameof(IsInputEnabled));
        }
    }

    /// <summary>Convenience inverse of <see cref="IsReadOnly"/> for binding input enablement.</summary>
    public bool IsInputEnabled => !_isReadOnly;

    private bool _isRawEditing;

    /// <summary>
    /// Whether this prompt is currently showing the plain text field instead of the
    /// widget its type hint suggests.
    /// </summary>
    /// <remarks>
    /// A hint suggests an affordance; it never restricts what may be entered. Showing the
    /// widget and a raw text box side by side made that promise visible but cluttered
    /// every field with a box most people never need. Showing the widget alone with a way
    /// through to raw text keeps the promise and the calm: a date picker for the common
    /// case, and "the summer of 1985" still typeable by anyone who needs it.
    ///
    /// This is per-prompt and per-session. It is a view state, never stored - nothing
    /// about how someone chose to enter an answer belongs in the document.
    /// </remarks>
    public bool IsRawEditing
    {
        get => _isRawEditing;
        private set
        {
            if (_isRawEditing == value) return;
            _isRawEditing = value;
            Notify(nameof(IsRawEditing));
            Notify(nameof(ShowHintedWidget));
            Notify(nameof(ShowRawEditor));
            Notify(nameof(ShowRawToggle));
            OnDerivedPropertiesShouldRefresh();
            Notify(nameof(RawToggleName));
            Notify(nameof(RawToggleGlyph));
            Notify(nameof(ToggleGlyphSize));
            Notify(nameof(ToggleButtonSize));
        }
    }

    /// <summary>Whether the type-hinted widget should be shown rather than the text field.</summary>
    /// <summary>Whether this prompt's suggested widget can actually be shown right now.</summary>
    /// <remarks>
    /// Some widgets are affordance-gated: a calendar picker, boolean radios and the
    /// preview formats only appear when the matching capability profile is on. Overridden
    /// by those view models so the base can tell the difference between "the user asked
    /// for the plain box" and "there is no widget to show them".
    /// </remarks>
    protected virtual bool HintedWidgetAvailable => true;

    /// <summary>Whether the suggested widget is on screen.</summary>
    public bool ShowHintedWidget =>
        RawEditorPresentation.ShowHintedWidget(_isRawEditing, HintedWidgetAvailable);

    /// <summary>Whether the plain text box is on screen.</summary>
    /// <remarks>
    /// The text box is the universal core, not one of two alternatives: any string is a
    /// valid response, and typing one must always be possible. So it appears whenever the
    /// suggested widget is not there - because the user asked for it, or because the
    /// widget was never available.
    ///
    /// Binding it to IsRawEditing alone left a date field with a hidden text box, a hidden
    /// picker and nothing to type into whenever the calendar affordance was off. Only
    /// visible by looking at a rendered frame; every test passed.
    /// </remarks>
    public bool ShowRawEditor =>
        RawEditorPresentation.ShowRawEditor(_isRawEditing, HintedWidgetAvailable);

    /// <summary>Whether to offer the widget/text toggle at all.</summary>
    /// <remarks>With no widget to switch to, the button would do nothing worth doing.</remarks>
    public bool ShowRawToggle => RawEditorPresentation.ShowRawToggle(HintedWidgetAvailable);

    /// <summary>
    /// Glyph for the toggle. Deliberately not the only signal: the button also carries an
    /// accessible name and tooltip describing what activating it will do, because a glyph
    /// alone is unreadable to a screen reader and ambiguous to everyone else.
    /// </summary>
    /// <summary>The toggle's glyph, pinned to text presentation.</summary>
    /// <remarks>
    /// U+FE0E is the variation selector that asks for the monochrome text form. Without
    /// it macOS renders both of these as full-colour emoji, which is the single most
    /// toy-looking element in an otherwise plain interface.
    /// </remarks>
    public string RawToggleGlyph => RawEditorPresentation.ToggleGlyph(_isRawEditing);

    /// <summary>
    /// Point size for the toggle glyph, sized to fill its button rather than sit as a
    /// speck in the middle of it.
    /// </summary>
    /// <remarks>
    /// Derived from the profile's text scale rather than hard-coded, so the icon grows
    /// with LargeText like everything else. A control that ignores the scale becomes the
    /// one thing a low-vision user cannot read.
    /// </remarks>
    public double ToggleGlyphSize => RawEditorPresentation.ToggleGlyphSize(ActiveProfile.TextScale);

    /// <summary>Side length of the toggle button, kept proportional to its glyph.</summary>
    public double ToggleButtonSize => RawEditorPresentation.ToggleButtonSize(ToggleGlyphSize);

    /// <summary>Accessible name for the toggle, describing what activating it will do.</summary>
    public string RawToggleName => RawEditorPresentation.ToggleName(_isRawEditing, Label);

    /// <summary>Flips between the hinted widget and the plain text field.</summary>
    public void ToggleRawEditing() => IsRawEditing = !_isRawEditing;

    /// <summary>Command form of <see cref="ToggleRawEditing"/> for view binding.</summary>
    public System.Windows.Input.ICommand ToggleRawEditingCommand =>
        _toggleRawEditingCommand ??= new CommunityToolkit.Mvvm.Input.RelayCommand(ToggleRawEditing);

    private System.Windows.Input.ICommand? _toggleRawEditingCommand;

    /// <summary>
    /// Re-reads the response from the underlying model and pulses the derived
    /// display properties. Called after expression recompute writes a computed
    /// value straight to the model (bypassing the VM setter).
    /// </summary>
    public void RefreshFromModel()
    {
        _responseState.RefreshFromModel();
    }

    /// <summary>
    /// Profile-aware rendered string for read-only display. Returns the raw response
    /// unchanged on the Default profile and on any non-formatting composition.
    /// </summary>
    public string DisplayValue
    {
        get
        {
            var formatted = _profileService.ActiveProfile.FormatDisplay(Response, ExpectedDataType);
            return formatted ?? string.Empty;
        }
    }

    /// <summary>The current active rendering profile; subclasses use this for type-specific rendering hints.</summary>
    protected IRenderingProfile ActiveProfile => _profileService.ActiveProfile;

    /// <summary>Profile service exposed to views for capability gating of UX affordances
    /// (input masks, calendar pickers, …). The view-layer concern stays in the view —
    /// the VM just hands over the gate.</summary>
    internal IProfileService ProfileService => _profileService;

    /// <summary>Hook subclasses override to pulse derived bool/string properties
    /// (e.g. ShowCalendarPicker, ShowDisplaysAs) when either the active profile
    /// changes OR the response changes. The base raises this on both events.</summary>
    protected virtual void OnDerivedPropertiesShouldRefresh() { }

    protected void Notify([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _profileRefresh.Dispose();
        GC.SuppressFinalize(this);
    }
}
