using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Date-hinted prompt. Stored as a raw ISO 8601 (YYYY-MM-DD) string when the user
/// types one, but free-text "see attached" / "TBD" / "approximately last week" are
/// also valid. VisualFormatting profile renders ISO dates as human-readable text
/// ("April 29, 2025") for sighted users.
/// </summary>
public sealed class DatePromptViewModel : PromptViewModelBase
{
    public DatePromptViewModel(Prompt prompt, IProfileService profileService)
        : base(prompt, profileService) { }
}
