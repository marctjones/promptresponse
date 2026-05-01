namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Input-mask flag: when active, "zipcode"/"zip"/"postalcode"-hinted text fields
/// reshape 9-digit input to "#####-####" (5-digit codes are left raw). Foreign
/// postal codes ("EC1A 1BB", "M5V 3M8") pass through.
/// </summary>
public sealed class ZipInputMaskProfile : RenderingProfileBase
{
    public override string Name => "ZipInputMask";
}
