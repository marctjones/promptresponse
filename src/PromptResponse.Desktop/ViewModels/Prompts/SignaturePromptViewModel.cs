using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Signature prompt: a text input where the user types their full legal name.
/// No cryptographic signing involved — this is a UI affordance for collecting a
/// typed-name signature on a paper-style form.
/// </summary>
public sealed class SignaturePromptViewModel : PromptViewModelBase
{
    public SignaturePromptViewModel(Prompt prompt, IProfileService profileService)
        : base(prompt, profileService) { }
}
