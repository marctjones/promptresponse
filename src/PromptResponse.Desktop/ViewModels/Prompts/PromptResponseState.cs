using PromptResponse.Core.Models;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Owns response mutation and the notification policy shared by every prompt
/// view model. A response remains arbitrary text; this state never validates or
/// coerces it from the prompt's advisory data-type hint.
/// </summary>
internal sealed class PromptResponseState
{
    private readonly Prompt _prompt;
    private readonly Action<string> _notify;
    private readonly Action _refreshDerived;

    public PromptResponseState(Prompt prompt, Action<string> notify, Action refreshDerived)
    {
        _prompt = prompt;
        _notify = notify;
        _refreshDerived = refreshDerived;
    }

    public string Response
    {
        get => _prompt.Response ?? string.Empty;
        set
        {
            var newValue = value ?? string.Empty;
            if (_prompt.Response == newValue) return;

            // The model setter clears computed-value provenance for authored text.
            _prompt.Response = newValue;
            _notify(nameof(Response));
            _notify(nameof(PromptViewModelBase.DisplayValue));
            NotifyProvenance();
            _refreshDerived();
        }
    }

    /// <summary>Refreshes view state after a model-side expression recompute.</summary>
    public void RefreshFromModel()
    {
        _notify(nameof(Response));
        _notify(nameof(PromptViewModelBase.DisplayValue));
        NotifyProvenance();
        _refreshDerived();
    }

    /// <summary>Refreshes formatting-dependent state after a hint or profile change.</summary>
    public void RefreshDisplayAndDerivedState()
    {
        _notify(nameof(PromptViewModelBase.DisplayValue));
        _refreshDerived();
    }

    private void NotifyProvenance()
    {
        _notify(nameof(PromptViewModelBase.ValueIsCalculated));
        _notify(nameof(PromptViewModelBase.ValueWasOverridden));
        _notify(nameof(PromptViewModelBase.ShowProvenanceMark));
        _notify(nameof(PromptViewModelBase.ProvenanceLabel));
        _notify(nameof(PromptViewModelBase.ProvenanceAnnouncement));
        _notify(nameof(PromptViewModelBase.ProvenanceColorCue));
    }
}
