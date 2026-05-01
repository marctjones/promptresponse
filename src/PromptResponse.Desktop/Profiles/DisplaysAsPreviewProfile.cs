namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Display-affordance flag: when active, prompts with a non-empty
/// <see cref="ViewModels.Prompts.PromptViewModelBase.DisplayValue"/> render a
/// "Displays as: …" live region under the input field. Helps both sighted users
/// (visual confirmation of formatting) and screen-reader users (polite-region
/// announcement on commit).
/// </summary>
public sealed class DisplaysAsPreviewProfile : RenderingProfileBase
{
    public override string Name => "DisplaysAsPreview";
}
