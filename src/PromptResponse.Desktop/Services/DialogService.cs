using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using PromptResponse.Core.Rendering;
using PromptResponse.Desktop.Services.Dialogs;
using PromptResponse.Rendering.Pdf;

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

        var dialog = DialogWindowFactory.Create(title, 450, 200, 350, 150, false, $"Confirmation Dialog: {title}", message);

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

    /// <inheritdoc/>
    public async Task<string?> ShowInputAsync(string title, string message, string defaultValue = "", bool isPassword = false)
    {
        _logger.LogDebug("Showing input dialog: {Title}", title);

        var window = GetMainWindow();
        if (window == null) return null;

        string? result = null;

        var dialog = DialogWindowFactory.Create(title, 460, 210, 360, 170, false, $"Input dialog: {title}", message);

        var label = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        };

        var input = new TextBox
        {
            Text = defaultValue,
            PasswordChar = isPassword ? '•' : '\0',
            MinWidth = 300,
        };
        input.SetValue(AutomationProperties.NameProperty, title);
        input.SetValue(AutomationProperties.HelpTextProperty, message);

        var okButton = new Button { Content = "OK", MinWidth = 80, Margin = new Thickness(0, 0, 10, 0), IsDefault = true };
        okButton.SetValue(AutomationProperties.NameProperty, "Confirm input");
        okButton.Click += (_, _) => { result = input.Text ?? string.Empty; dialog.Close(); };

        var cancelButton = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
        cancelButton.SetValue(AutomationProperties.NameProperty, "Cancel input");
        cancelButton.Click += (_, _) => { result = null; dialog.Close(); };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
            Children = { okButton, cancelButton },
        };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Children = { label, input, buttons },
        };

        await dialog.ShowDialog(window);
        return result;
    }

    /// <inheritdoc/>
    public async Task<int?> ShowChoiceAsync(string title, string message, IReadOnlyList<string> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        if (choices.Count == 0) return null;

        var window = GetMainWindow();
        if (window == null) return null;
        int? result = null;
        var dialog = DialogWindowFactory.Create(title, 560, 280, 400, 220, true, title, message);

        var options = new StackPanel { Spacing = 6 };
        for (var index = 0; index < choices.Count; index++)
        {
            var captured = index;
            var option = new RadioButton
            {
                Content = choices[index],
                GroupName = "dialog-choice",
                IsChecked = index == 0,
            };
            option.SetValue(AutomationProperties.NameProperty, choices[index]);
            option.SetValue(AutomationProperties.HelpTextProperty, $"Submission destination {index + 1} of {choices.Count}");
            option.IsCheckedChanged += (_, _) => { if (option.IsChecked == true) result = captured; };
            options.Children.Add(option);
        }
        result = 0;

        var select = new Button { Content = "Continue", MinWidth = 96, IsDefault = true };
        select.SetValue(AutomationProperties.NameProperty, "Continue with selected destination");
        select.Click += (_, _) => dialog.Close();
        var cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true, Margin = new Thickness(10, 0, 0, 0) };
        cancel.SetValue(AutomationProperties.NameProperty, "Cancel submission");
        cancel.Click += (_, _) => { result = null; dialog.Close(); };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), Children = { select, cancel } };
        var content = new StackPanel { Margin = new Thickness(20), Spacing = 8 };
        content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new ScrollViewer { Content = options, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        content.Children.Add(buttons);
        dialog.Content = content;
        await dialog.ShowDialog(window);
        return result;
    }

    /// <inheritdoc/>
    public async Task ShowPrintPreviewAsync(RenderModel model, bool includeEmptyFields)
    {
        ArgumentNullException.ThrowIfNull(model);
        _logger.LogDebug("Showing print preview for {Title}", model.Title);

        var window = GetMainWindow();
        if (window == null) return;

        var dialog = DialogWindowFactory.Create(
            "Print Preview", 820, 760, 620, 520, true, "Print preview",
            "Preview generated print content before exporting to PDF");

        var closeButton = new Button
        {
            Content = "Close",
            MinWidth = 96,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsDefault = true,
            IsCancel = true,
        };
        closeButton.SetValue(AutomationProperties.NameProperty, "Close print preview");
        closeButton.Click += (_, _) => dialog.Close();

        var preview = PrintPreviewContentBuilder.Build(model, includeEmptyFields);
        var scroll = new ScrollViewer
        {
            Content = preview,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        scroll.SetValue(AutomationProperties.NameProperty, "Print preview content");

        var buttonBar = new Border
        {
            Padding = new Thickness(16, 10),
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = Brushes.LightGray,
            Child = closeButton,
        };
        DockPanel.SetDock(buttonBar, Dock.Bottom);

        dialog.Content = new DockPanel
        {
            LastChildFill = true,
            Children =
            {
                buttonBar,
                scroll,
            },
        };

        await dialog.ShowDialog(window);
    }

    /// <inheritdoc/>
    public async Task<bool> ShowImportReviewAsync(ImportQuality quality)
    {
        ArgumentNullException.ThrowIfNull(quality);
        _logger.LogDebug("Showing import review dialog: {Score}", quality.Score);

        var window = GetMainWindow();
        if (window == null) return false;

        var result = false;
        var dialog = DialogWindowFactory.Create(
            "Review PDF Import", 680, 560, 560, 420, true, "PDF import review", quality.Summary);

        var openButton = new Button
        {
            Content = "Open Anyway",
            MinWidth = 120,
            IsDefault = true,
        };
        openButton.SetValue(AutomationProperties.NameProperty, "Open imported template anyway");
        openButton.SetValue(AutomationProperties.HelpTextProperty, "Open the imported template so you can fix labels and field types manually");
        openButton.Click += (_, _) => { result = true; dialog.Close(); };

        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 96,
            IsCancel = true,
            Margin = new Thickness(8, 0, 0, 0),
        };
        cancelButton.SetValue(AutomationProperties.NameProperty, "Cancel import");
        cancelButton.SetValue(AutomationProperties.HelpTextProperty, "Do not open this low-quality import");
        cancelButton.Click += (_, _) => { result = false; dialog.Close(); };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { openButton, cancelButton },
        };

        var buttonBar = new Border
        {
            Padding = new Thickness(16, 10),
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = Brushes.LightGray,
            Child = buttons,
        };
        DockPanel.SetDock(buttonBar, Dock.Bottom);

        var content = ImportReviewContentBuilder.Build(quality);
        var scroll = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        scroll.SetValue(AutomationProperties.NameProperty, "PDF import review content");

        dialog.Content = new DockPanel
        {
            LastChildFill = true,
            Children = { buttonBar, scroll },
        };

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
