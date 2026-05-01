namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Input-mask flag: when active, "phone"-hinted text fields reshape digit-only
/// input as the user types ("(555) 123-4567"). Free text ("see HR") passes
/// through unchanged.
/// </summary>
public sealed class PhoneInputMaskProfile : RenderingProfileBase
{
    public override string Name => "PhoneInputMask";
}
