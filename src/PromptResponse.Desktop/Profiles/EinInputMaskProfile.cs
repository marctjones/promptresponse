namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Input-mask flag: when active, "ein"-hinted text fields reshape digit-only
/// input as the user types ("##-#######"). Free text passes through.
/// </summary>
public sealed class EinInputMaskProfile : RenderingProfileBase
{
    public override string Name => "EinInputMask";
}
