namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Interactive-widget flag: when active, Boolean-hinted prompts render auxiliary
/// Yes/No radio buttons beside the free-text field. The text field always remains
/// the source of truth — "maybe", "depends" stay valid responses.
/// </summary>
public sealed class BooleanRadiosProfile : RenderingProfileBase
{
    public override string Name => "BooleanRadios";
}
