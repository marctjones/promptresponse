using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Editing;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Number-hinted prompt. Rendering: VisualFormatting profile renders "42000" as
/// "42,000" (or culture equivalent) for sighted users while preserving the raw
/// "42000" in the underlying response. Non-numeric responses pass through unchanged
/// — "five" is still a valid response.
/// </summary>
public sealed class NumberPromptViewModel : PromptViewModelBase
{
    public NumberPromptViewModel(Prompt prompt, IProfileService profileService, EditHistory? history = null)
        : base(prompt, profileService, history) { }

    /// <summary>True when both <see cref="DisplaysAsPreviewProfile"/> is active AND
    /// <see cref="PromptViewModelBase.DisplayValue"/> differs meaningfully — i.e.
    /// some active flag actually reformatted the response.</summary>
    public bool ShowDisplaysAs =>
        ProfileService.IsActive(typeof(DisplaysAsPreviewProfile))
        && !string.IsNullOrEmpty(DisplayValue)
        && DisplayValue != Response;

    protected override void OnDerivedPropertiesShouldRefresh() => Notify(nameof(ShowDisplaysAs));
}
