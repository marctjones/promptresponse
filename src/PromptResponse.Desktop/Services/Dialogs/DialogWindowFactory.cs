using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace PromptResponse.Desktop.Services.Dialogs;

/// <summary>
/// Creates consistently configured, owner-centered application dialogs.
/// </summary>
internal static class DialogWindowFactory
{
    public static Window Create(
        string title,
        double width,
        double height,
        double minWidth,
        double minHeight,
        bool canResize,
        string automationName,
        string automationHelpText)
    {
        var dialog = new Window
        {
            Title = title,
            Width = width,
            Height = height,
            MinWidth = minWidth,
            MinHeight = minHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = canResize,
            ShowInTaskbar = false,
        };

        dialog.SetValue(AutomationProperties.NameProperty, automationName);
        dialog.SetValue(AutomationProperties.HelpTextProperty, automationHelpText);
        return dialog;
    }
}
