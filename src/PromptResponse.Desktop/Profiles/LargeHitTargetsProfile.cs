using Avalonia;

namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Motor-axis flag: when active, the universal touch-target floor rises to 56×56 px
/// so tremor / switch / voice-control / one-handed users have larger hit areas.
/// Replaces the single-affordance <c>MotorAssistProfile</c> with a clearer name
/// matching the underlying capability.
/// </summary>
public sealed class LargeHitTargetsProfile : RenderingProfileBase
{
    public override string Name => "LargeHitTargets";
    public override Size MinimumTouchTarget => new(56, 56);
}
