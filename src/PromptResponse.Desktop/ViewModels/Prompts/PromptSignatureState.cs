using PromptResponse.Core.Signing;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>Owns covering-signature state and its dependent binding notifications.</summary>
internal sealed class PromptSignatureState(Action<string> notify)
{
    private IReadOnlyList<CoveringSignature> _covering = [];

    internal IReadOnlyList<CoveringSignature> CoveringSignatures
    {
        get => _covering;
        set
        {
            _covering = value ?? [];
            notify(nameof(CoveringSignatures));
            notify(nameof(PromptViewModelBase.SignatureState));
            notify(nameof(PromptViewModelBase.ShowSignatureMark));
            notify(nameof(PromptViewModelBase.SignatureLabel));
            notify(nameof(PromptViewModelBase.SignatureAnnouncement));
            notify(nameof(PromptViewModelBase.SignatureIsBroken));
        }
    }
}
