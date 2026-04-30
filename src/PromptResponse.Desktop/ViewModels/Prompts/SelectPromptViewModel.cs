using System.Collections.ObjectModel;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Single-choice selection prompt. The view renders the suggested values as radio
/// buttons / dropdown / etc. The user can also type a value not in the list — per
/// the vision, any text is a valid response.
/// </summary>
public sealed class SelectPromptViewModel : PromptViewModelBase
{
    public SelectPromptViewModel(Prompt prompt, IProfileService profileService)
        : base(prompt, profileService)
    {
        SuggestedValues = new ReadOnlyCollection<string>(prompt.Hints.SuggestedValues ?? new List<string>());
    }

    /// <summary>The pre-populated suggestion list from the prompt's hints.</summary>
    public IReadOnlyList<string> SuggestedValues { get; }
}
