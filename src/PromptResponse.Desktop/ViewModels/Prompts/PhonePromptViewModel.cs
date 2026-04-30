using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.ViewModels.Prompts;

public sealed class PhonePromptViewModel : PromptViewModelBase
{
    public PhonePromptViewModel(Prompt prompt, IProfileService profileService)
        : base(prompt, profileService) { }
}
