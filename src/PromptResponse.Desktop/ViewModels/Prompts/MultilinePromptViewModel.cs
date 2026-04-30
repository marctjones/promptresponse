using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Multi-line free-text prompt. Identical semantics to <see cref="TextPromptViewModel"/>
/// but renders as a multi-line text area in the view.
/// </summary>
public sealed class MultilinePromptViewModel : PromptViewModelBase
{
    public MultilinePromptViewModel(Prompt prompt, IProfileService profileService)
        : base(prompt, profileService) { }
}
