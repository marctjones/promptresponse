using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Extensions.Logging;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Implementation of dialog service using Avalonia windows.
/// </summary>
public class DialogService : IDialogService
{
    private readonly ILogger<DialogService> _logger;

    public DialogService(ILogger<DialogService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        _logger.LogDebug("Showing confirmation dialog: {Title}", title);
        return await ShowConfirmationDialogAsync(title, message);
    }

    private static async Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        var window = GetMainWindow();
        if (window == null) return false;

        var result = false;

        var dialog = new Window
        {
            Title = title,
            Width = 450,
            Height = 200,
            MinWidth = 350,
            MinHeight = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
        };

        dialog.SetValue(AutomationProperties.NameProperty, $"Confirmation Dialog: {title}");
        dialog.SetValue(AutomationProperties.HelpTextProperty, message);

        var yesButton = new Button
        {
            Content = "Yes",
            MinWidth = 80,
            Margin = new Thickness(0, 0, 10, 0),
        };
        yesButton.SetValue(AutomationProperties.NameProperty, "Confirm action");
        yesButton.SetValue(AutomationProperties.HelpTextProperty, "Click to confirm and proceed");
        yesButton.Click += (s, e) =>
        {
            result = true;
            dialog.Close();
        };

        var noButton = new Button
        {
            Content = "No",
            MinWidth = 80,
        };
        noButton.SetValue(AutomationProperties.NameProperty, "Cancel action");
        noButton.SetValue(AutomationProperties.HelpTextProperty, "Click to cancel and go back");
        noButton.Click += (s, e) =>
        {
            result = false;
            dialog.Close();
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0),
            Children = { yesButton, noButton },
        };

        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 10),
        };
        messageText.SetValue(AutomationProperties.NameProperty, "Confirmation message");
        messageText.SetValue(AutomationProperties.HelpTextProperty, message);

        var contentPanel = new StackPanel
        {
            Margin = new Thickness(20),
            Children =
            {
                messageText,
                buttonPanel,
            },
        };

        dialog.Content = contentPanel;

        await dialog.ShowDialog(window);
        return result;
    }

    private static Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }
}
