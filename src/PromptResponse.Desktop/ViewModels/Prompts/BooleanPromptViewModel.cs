using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Boolean / yes-no prompt. Stored as a string ("yes" / "no" / "true" / "false" or
/// any free-text response — even "maybe", "depends" are accepted per the vision).
/// The view exposes a tri-state: clear / yes / no.
/// </summary>
public sealed class BooleanPromptViewModel : PromptViewModelBase
{
    public BooleanPromptViewModel(Prompt prompt, IProfileService profileService)
        : base(prompt, profileService) { }

    /// <summary>True when the active capability profile includes the Boolean-radios
    /// affordance. Bound to the radios' IsVisible — universal-core (no flag active)
    /// shows just the free-text field.</summary>
    public bool ShowRadios => ProfileService.IsActive(typeof(BooleanRadiosProfile));

    protected override void OnDerivedPropertiesShouldRefresh() => Notify(nameof(ShowRadios));

    /// <summary>
    /// Three-state boolean view: true if response parses as a yes-equivalent,
    /// false for no-equivalent, null when the response is empty or any free-text
    /// value the parser doesn't recognise.
    /// </summary>
    public bool? IsTrue
    {
        get => Response.Trim().ToLowerInvariant() switch
        {
            "yes" or "true" or "1" or "on" => true,
            "no" or "false" or "0" or "off" => false,
            _ => null,
        };
        set
        {
            Response = value switch
            {
                true => "yes",
                false => "no",
                null => string.Empty,
            };
            Notify(nameof(IsTrue));
        }
    }
}
