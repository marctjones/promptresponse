using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Number-hinted prompt. Rendering: VisualFormatting profile renders "42000" as
/// "42,000" (or culture equivalent) for sighted users while preserving the raw
/// "42000" in the underlying response. Non-numeric responses pass through unchanged
/// — "five" is still a valid response.
/// </summary>
public sealed class NumberPromptViewModel : PromptViewModelBase
{
    public NumberPromptViewModel(Prompt prompt, IProfileService profileService)
        : base(prompt, profileService) { }
}
