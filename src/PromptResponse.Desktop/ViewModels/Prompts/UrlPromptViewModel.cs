using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.ViewModels.Prompts;

public sealed class UrlPromptViewModel : PromptViewModelBase
{
    public UrlPromptViewModel(Prompt prompt, IProfileService profileService)
        : base(prompt, profileService) { }
}
