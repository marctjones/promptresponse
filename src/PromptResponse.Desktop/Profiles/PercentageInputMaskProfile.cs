namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Input-mask flag: when active, "percentage"/"percent"-hinted text fields append
/// "%" on commit (LostFocus) when the input parses as a number. Free text passes
/// through. Commit-time only — trailing-% on every keystroke fights typing.
/// </summary>
public sealed class PercentageInputMaskProfile : RenderingProfileBase
{
    public override string Name => "PercentageInputMask";
}
