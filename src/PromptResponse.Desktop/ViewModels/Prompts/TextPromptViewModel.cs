using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Editing;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Plain-text single-line prompt. The default for prompts with no expectedDataType
/// or with "text" / unknown types.
/// </summary>
public sealed class TextPromptViewModel : PromptViewModelBase
{
    public TextPromptViewModel(Prompt prompt, IProfileService profileService, EditHistory? history = null)
        : base(prompt, profileService, history) { }
}
