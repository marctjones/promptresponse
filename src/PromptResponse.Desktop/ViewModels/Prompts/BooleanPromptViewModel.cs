using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Editing;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Boolean / yes-no prompt. Stored as a string ("yes" / "no" / "true" / "false" or
/// any free-text response — even "maybe", "depends" are accepted per the vision).
/// The view exposes a tri-state: clear / yes / no.
/// </summary>
public sealed class BooleanPromptViewModel : PromptViewModelBase
{
    public BooleanPromptViewModel(Prompt prompt, IProfileService profileService, EditHistory? history = null)
        : base(prompt, profileService, history) { }

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
            // Canonical write form is "true"/"false" (specification section 4.9).
            // "yes" is English; a format that renders to speech, to other languages, and
            // into database columns cannot make its boolean depend on one of them. The
            // generous read set below still accepts yes/on/1/x and free text.
            Response = value switch
            {
                true => "true",
                false => "false",
                null => string.Empty,
            };
            Notify(nameof(IsTrue));
        }
    }
}
