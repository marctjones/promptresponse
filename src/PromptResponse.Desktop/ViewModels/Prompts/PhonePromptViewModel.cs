using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Editing;

namespace PromptResponse.Desktop.ViewModels.Prompts;

public sealed class PhonePromptViewModel : PromptViewModelBase
{
    public PhonePromptViewModel(Prompt prompt, IProfileService profileService, EditHistory? history = null)
        : base(prompt, profileService, history) { }
}
