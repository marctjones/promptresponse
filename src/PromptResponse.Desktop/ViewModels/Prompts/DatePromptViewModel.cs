using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Editing;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Date-hinted prompt. Stored as a raw ISO 8601 (YYYY-MM-DD) string when the user
/// types one, but free-text "see attached" / "TBD" / "approximately last week" are
/// also valid. The <see cref="IsoDatePrettifyProfile"/> flag renders ISO dates as
/// human-readable text ("April 29, 2025"); <see cref="CalendarPickerProfile"/> adds
/// an auxiliary picker widget beside the text field.
/// </summary>
public sealed class DatePromptViewModel : PromptViewModelBase
{
    public DatePromptViewModel(Prompt prompt, IProfileService profileService, EditHistory? history = null)
        : base(prompt, profileService, history) { }

    /// <summary>True when the active capability profile includes the calendar-picker
    /// affordance. Bound to the picker's IsVisible — universal-core (no flag active)
    /// shows just the text field.</summary>
    /// <summary>The picker is shown when its affordance is on and the user has not asked
    /// for the plain text box instead.</summary>
    public bool ShowCalendarPickerNow => ShowCalendarPicker && ShowHintedWidget;

    public bool ShowCalendarPicker => ProfileService.IsActive(typeof(CalendarPickerProfile));

    protected override void OnDerivedPropertiesShouldRefresh()
    {
        Notify(nameof(ShowCalendarPicker));
        Notify(nameof(ShowCalendarPickerNow));
    }
}
