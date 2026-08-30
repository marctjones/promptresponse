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

        dialog.Content = InteractiveDialogContentBuilder.BuildConfirmation(
            message,
            () => { result = true; dialog.Close(); },
            () => { result = false; dialog.Close(); });

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

        dialog.Content = InteractiveDialogContentBuilder.BuildInput(
            title,
            message,
            defaultValue,
            isPassword,
            value => { result = value; dialog.Close(); },
            () => { result = null; dialog.Close(); });

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

        result = 0;
        dialog.Content = InteractiveDialogContentBuilder.BuildChoice(
            message,
            choices,
            index =>
            {
                if (index >= 0) result = index;
                else dialog.Close();
            },
            () => { result = null; dialog.Close(); });
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
