using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.ViewModels.Prompts;

public sealed class EmailPromptViewModel : PromptViewModelBase
{
    public EmailPromptViewModel(Prompt prompt, IProfileService profileService)
        : base(prompt, profileService) { }
}
