using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Editing;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// File-attachment prompt: stores a relative file path string the user picks via
/// a file picker. Free-text is also accepted (vision invariant) — e.g. "see attached
/// PDF in email" — the view exposes both a "Browse" affordance and a text input.
/// </summary>
public sealed class FilePromptViewModel : PromptViewModelBase
{
    public FilePromptViewModel(Prompt prompt, IProfileService profileService, EditHistory? history = null)
        : base(prompt, profileService, history) { }

    public string FileName =>
        string.IsNullOrEmpty(Response) ? string.Empty : System.IO.Path.GetFileName(Response);
}
