using System.ComponentModel;
using System.Runtime.CompilerServices;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Editing;

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
    private bool _disposed;

    protected PromptViewModelBase(Prompt prompt, IProfileService profileService, EditHistory? history = null)
    {
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _history = history;
        _profileService.ProfileChanged += OnProfileChanged;
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
                Notify(nameof(DisplayValue));
                OnDerivedPropertiesShouldRefresh();
            },
            value);
    }

    /// <summary>Optional regex hint for advisory pattern validation.</summary>
    public string? ValidationPattern
    {
        get => _prompt.Hints.ValidationPattern;
        set => SetWithUndo(nameof(ValidationPattern), () => _prompt.Hints.ValidationPattern, v => _prompt.Hints.ValidationPattern = v, value);
    }

    /// <summary>Helper for property setters: routes through edit history when one
    /// is configured (and not currently applying) so the change is undoable +
    /// keystroke-mergeable; otherwise applies it directly.</summary>
    private void SetWithUndo<T>(string propertyName, Func<T> getter, Action<T> applySetter, T newValue)
    {
        var oldValue = getter();
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue)) return;

        if (_history?.IsApplying == true)
        {
            applySetter(newValue);
            Notify(propertyName);
            return;
        }
        if (_history != null)
        {
            _history.Execute(new PropertyEditCommand<T>(
                this, propertyName,
                v => { applySetter(v); Notify(propertyName); },
                oldValue, newValue));
        }
        else
        {
            applySetter(newValue);
            Notify(propertyName);
        }
    }

    /// <summary>Underlying model — exposed for the editor surface to access fields
    /// (e.g. SuggestedValues list) directly. Fill-mode rendering should not use this.</summary>
    internal Prompt Model => _prompt;

    /// <summary>
    /// Raw response string. Setting this updates the underlying <see cref="Prompt"/> model.
    /// Any visible text is a valid response (vision invariant).
    /// </summary>
    public string Response
    {
        get => _prompt.Response ?? string.Empty;
        set
        {
            var newValue = value ?? string.Empty;
            if (_prompt.Response == newValue) return;
            _prompt.Response = newValue;
            Notify(nameof(Response));
            Notify(nameof(DisplayValue));
            OnDerivedPropertiesShouldRefresh();
        }
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

    private void OnProfileChanged(object? sender, EventArgs e)
    {
        Notify(nameof(DisplayValue));
        OnDerivedPropertiesShouldRefresh();
    }

    protected void Notify([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _profileService.ProfileChanged -= OnProfileChanged;
        GC.SuppressFinalize(this);
    }
}
