namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Input-mask flag: when active, "ssn"-hinted text fields reshape digit-only
/// input as the user types ("###-##-####"). Free text passes through.
/// </summary>
public sealed class SsnInputMaskProfile : RenderingProfileBase
{
    public override string Name => "SsnInputMask";
}
