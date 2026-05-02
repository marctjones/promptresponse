using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Creates the right typed <see cref="PromptViewModelBase"/> subclass for a given
/// <see cref="Prompt"/> based on its <see cref="PromptHints.ExpectedDataType"/>.
/// Unknown / missing types fall back to <see cref="TextPromptViewModel"/>.
/// </summary>
public sealed class PromptViewModelFactory
{
    private readonly IProfileService _profileService;

    public PromptViewModelFactory(IProfileService profileService)
    {
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
    }

    public PromptViewModelBase Create(Prompt prompt)
    {
        if (prompt == null) throw new ArgumentNullException(nameof(prompt));

        var hint = prompt.Hints.ExpectedDataType?.ToLowerInvariant();
        return hint switch
        {
            "number" => new NumberPromptViewModel(prompt, _profileService),
            "currency" => new CurrencyPromptViewModel(prompt, _profileService),
            "date" => new DatePromptViewModel(prompt, _profileService),
            "datetime" => new DatePromptViewModel(prompt, _profileService),
            "time" => new TextPromptViewModel(prompt, _profileService),
            "email" => new EmailPromptViewModel(prompt, _profileService),
            "url" => new UrlPromptViewModel(prompt, _profileService),
            "phone" => new PhonePromptViewModel(prompt, _profileService),
            "boolean" => new BooleanPromptViewModel(prompt, _profileService),
            "multiline" => new MultilinePromptViewModel(prompt, _profileService),
            "signature" => new SignaturePromptViewModel(prompt, _profileService),
            "file" => new FilePromptViewModel(prompt, _profileService),
            "select" => new SelectPromptViewModel(prompt, _profileService),
            "multichoice" => new MultichoicePromptViewModel(prompt, _profileService),
            _ when prompt.Hints.SuggestedValues is { Count: > 0 } => new SelectPromptViewModel(prompt, _profileService),
            _ => new TextPromptViewModel(prompt, _profileService),
        };
    }
}
