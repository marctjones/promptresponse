using System.Globalization;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.Views.Prompts;

public partial class DatePromptView : UserControl
{
    public DatePromptView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SyncPickerFromResponse();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnSelectedDateChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (DataContext is not DatePromptViewModel vm) return;
        if (sender is not CalendarDatePicker picker) return;
        // Picker drives the Response when used. Free-text typed into the TextBox
        // overrides via two-way binding; we don't fight it here.
        if (picker.SelectedDate is { } date)
        {
            vm.Response = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }

    private void SyncPickerFromResponse()
    {
        if (DataContext is not DatePromptViewModel vm) return;
        var picker = this.FindControl<CalendarDatePicker>("DatePicker");
        if (picker is null) return;

        // Best-effort: parse ISO-8601 from Response. Free-text is also valid;
        // the picker stays clear when Response isn't a recognisable date.
        if (DateTime.TryParseExact(vm.Response, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
        {
            picker.SelectedDate = parsed;
        }
        else
        {
            picker.SelectedDate = null;
        }
    }
}
