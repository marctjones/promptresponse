namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Input-mask flag: when active, "currency"-hinted text fields reshape decimal
/// input on commit (LostFocus) to the active culture's currency form ("$1,234.56").
/// Free text ("varies", "see notes") passes through. Commit-time only — live
/// reshape on every keystroke would fight typing decimals.
/// </summary>
public sealed class CurrencyInputMaskProfile : RenderingProfileBase
{
    public override string Name => "CurrencyInputMask";
}
