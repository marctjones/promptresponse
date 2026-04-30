using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Currency-hinted prompt. Stored response is the raw user input. VisualFormatting
/// profile renders numeric values with the user's culture currency symbol ("$42,000.00");
/// non-numeric responses ("varies", "see notes") pass through unchanged.
/// </summary>
public sealed class CurrencyPromptViewModel : PromptViewModelBase
{
    public CurrencyPromptViewModel(Prompt prompt, IProfileService profileService)
        : base(prompt, profileService) { }
}
