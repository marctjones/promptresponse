using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Editing;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Creates the right typed <see cref="PromptViewModelBase"/> subclass for a given
/// <see cref="Prompt"/> based on its <see cref="PromptHints.ExpectedDataType"/>.
/// Unknown / missing types fall back to <see cref="TextPromptViewModel"/>.
/// </summary>
/// <remarks>
/// When constructed with an <see cref="EditHistory"/>, every prompt VM threads
/// it through so settable properties (Label, Hints fields, etc.) record their
/// edits as undoable commands. Tests that don't need undo can pass null.
/// </remarks>
public sealed class PromptViewModelFactory
{
    private readonly IProfileService _profileService;
    private readonly EditHistory? _history;

    public PromptViewModelFactory(IProfileService profileService, EditHistory? history = null)
    {
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _history = history;
    }

    public PromptViewModelBase Create(Prompt prompt)
    {
        if (prompt == null) throw new ArgumentNullException(nameof(prompt));

        var hint = prompt.Hints.ExpectedDataType?.ToLowerInvariant();
        return hint switch
        {
            "number" => new NumberPromptViewModel(prompt, _profileService, _history),
            "currency" => new CurrencyPromptViewModel(prompt, _profileService, _history),
            "date" => new DatePromptViewModel(prompt, _profileService, _history),
            "datetime" => new DatePromptViewModel(prompt, _profileService, _history),
            "time" => new TextPromptViewModel(prompt, _profileService, _history),
            "email" => new EmailPromptViewModel(prompt, _profileService, _history),
            "url" => new UrlPromptViewModel(prompt, _profileService, _history),
            "phone" => new PhonePromptViewModel(prompt, _profileService, _history),
            "boolean" => new BooleanPromptViewModel(prompt, _profileService, _history),
            "multiline" => new MultilinePromptViewModel(prompt, _profileService, _history),
            "signature" => new SignaturePromptViewModel(prompt, _profileService, _history),
            "file" => new FilePromptViewModel(prompt, _profileService, _history),
            "select" => new SelectPromptViewModel(prompt, _profileService, _history),
            "multichoice" => new MultichoicePromptViewModel(prompt, _profileService, _history),
            _ when prompt.Hints.SuggestedValues is { Count: > 0 } => new SelectPromptViewModel(prompt, _profileService, _history),
            _ => new TextPromptViewModel(prompt, _profileService, _history),
        };
    }
}
