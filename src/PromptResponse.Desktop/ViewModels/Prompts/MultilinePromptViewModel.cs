using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Editing;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Multi-line free-text prompt. Identical semantics to <see cref="TextPromptViewModel"/>
/// but renders as a multi-line text area in the view.
/// </summary>
public sealed class MultilinePromptViewModel : PromptViewModelBase
{
    public MultilinePromptViewModel(Prompt prompt, IProfileService profileService, EditHistory? history = null)
        : base(prompt, profileService, history) { }
}
