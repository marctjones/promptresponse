namespace PromptResponse.Desktop.Profiles;

/// <summary>
/// Interactive-widget flag: when active, Date-hinted prompts render an auxiliary
/// <see cref="Avalonia.Controls.CalendarDatePicker"/> beside the text field. The
/// text field always remains the source of truth — picker selections write to
/// the response as ISO date strings; arbitrary free text in the field passes
/// through untouched.
/// </summary>
public sealed class CalendarPickerProfile : RenderingProfileBase
{
    public override string Name => "CalendarPicker";
}
