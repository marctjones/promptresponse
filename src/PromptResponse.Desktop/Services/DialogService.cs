using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using PromptResponse.Core.Rendering;
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

    /// <inheritdoc/>
    public async Task<string?> ShowInputAsync(string title, string message, string defaultValue = "", bool isPassword = false)
    {
        _logger.LogDebug("Showing input dialog: {Title}", title);

        var window = GetMainWindow();
        if (window == null) return null;

        string? result = null;

        var dialog = new Window
        {
            Title = title,
            Width = 460,
            Height = 210,
            MinWidth = 360,
            MinHeight = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
        };
        dialog.SetValue(AutomationProperties.NameProperty, $"Input dialog: {title}");
        dialog.SetValue(AutomationProperties.HelpTextProperty, message);

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
        var dialog = new Window
        {
            Title = title,
            Width = 560,
            Height = 280,
            MinWidth = 400,
            MinHeight = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true,
            ShowInTaskbar = false,
        };
        dialog.SetValue(AutomationProperties.NameProperty, title);
        dialog.SetValue(AutomationProperties.HelpTextProperty, message);

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

        var dialog = new Window
        {
            Title = "Print Preview",
            Width = 820,
            Height = 760,
            MinWidth = 620,
            MinHeight = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true,
            ShowInTaskbar = false,
        };
        dialog.SetValue(AutomationProperties.NameProperty, "Print preview");
        dialog.SetValue(AutomationProperties.HelpTextProperty, "Preview generated print content before exporting to PDF");

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

        var preview = BuildPreviewContent(model, includeEmptyFields);
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
        var dialog = new Window
        {
            Title = "Review PDF Import",
            Width = 680,
            Height = 560,
            MinWidth = 560,
            MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true,
            ShowInTaskbar = false,
        };
        dialog.SetValue(AutomationProperties.NameProperty, "PDF import review");
        dialog.SetValue(AutomationProperties.HelpTextProperty, quality.Summary);

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

        var content = BuildImportReviewContent(quality);
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

    private static Control BuildImportReviewContent(ImportQuality quality)
    {
        var panel = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(24),
        };

        panel.Children.Add(new TextBlock
        {
            Text = "PDF Import Needs Review",
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(new TextBlock
        {
            Text = quality.Summary,
            TextWrapping = TextWrapping.Wrap,
        });

        var readable = (int)Math.Round((1 - quality.CrypticLabelRatio) * 100);
        var tooltip = (int)Math.Round(quality.TooltipCoverage * 100);
        var duplicate = (int)Math.Round(quality.DuplicateLabelRatio * 100);
        panel.Children.Add(new TextBlock
        {
            Text = $"Score: {quality.Score}/100 ({quality.Grade})  -  Fields: {quality.FieldCount}  -  Readable labels: {readable}%  -  Tooltips: {tooltip}%  -  Duplicate labels: {duplicate}%",
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });

        panel.Children.Add(new TextBlock
        {
            Text = quality.Recommendation == ImportRecommendation.UseSkillInstead
                ? "Recommended next step: use the document-to-apr skill or the importer-to-skill hybrid workflow to enrich labels and sections while preserving imported field IDs."
                : "Recommended next step: open the template and review the flagged fields before sharing it.",
            TextWrapping = TextWrapping.Wrap,
        });

        var counts = quality.Flags
            .GroupBy(f => f.Kind)
            .OrderBy(g => g.Key.ToString())
            .Select(g => $"{LabelFor(g.Key)}: {g.Count()}");
        panel.Children.Add(new TextBlock
        {
            Text = quality.Flags.Count == 0
                ? "No field-level flags were reported."
                : "Flag summary: " + string.Join("  |  ", counts),
            TextWrapping = TextWrapping.Wrap,
        });

        if (quality.Flags.Count > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Sample fields to review",
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, 8, 0, 0),
            });

            foreach (var flag in quality.Flags.Take(12))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $"{LabelFor(flag.Kind)} - {flag.Label} ({flag.PromptId}): {flag.Message}",
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            if (quality.Flags.Count > 12)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $"...and {quality.Flags.Count - 12} more flagged fields.",
                    Foreground = Brushes.DimGray,
                    TextWrapping = TextWrapping.Wrap,
                });
            }
        }

        return panel;
    }

    private static string LabelFor(FieldFlagKind kind) => kind switch
    {
        FieldFlagKind.CrypticLabel => "Cryptic label",
        FieldFlagKind.DuplicateLabel => "Duplicate label",
        FieldFlagKind.AmbiguousChoice => "Ambiguous choice",
        _ => kind.ToString(),
    };

    private static Control BuildPreviewContent(RenderModel model, bool includeEmptyFields)
    {
        var page = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(28),
            MaxWidth = 720,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        page.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(model.Title) ? "(untitled)" : model.Title,
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
        });
        if (!string.IsNullOrWhiteSpace(model.Description))
        {
            page.Children.Add(new TextBlock
            {
                Text = model.Description,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.DimGray,
            });
        }

        page.Children.Add(new TextBlock
        {
            Text = $"PDF export preview - Letter page size - {(includeEmptyFields ? "blank fields included" : "blank fields excluded")}",
            FontSize = 12,
            Foreground = Brushes.DimGray,
            TextWrapping = TextWrapping.Wrap,
        });

        foreach (var block in model.Blocks)
        {
            AddBlock(page, block);
        }

        return page;
    }

    private static void AddBlock(StackPanel page, RenderBlock block)
    {
        switch (block)
        {
            case HeadingBlock h:
                page.Children.Add(new TextBlock
                {
                    Text = h.Text,
                    FontSize = h.Level == 1 ? 18 : 15,
                    FontWeight = FontWeight.SemiBold,
                    Margin = new Thickness(0, h.Level == 1 ? 14 : 8, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                });
                if (!string.IsNullOrWhiteSpace(h.Description))
                {
                    page.Children.Add(new TextBlock
                    {
                        Text = h.Description,
                        Foreground = Brushes.DimGray,
                        TextWrapping = TextWrapping.Wrap,
                    });
                }
                break;

            case FieldBlock f:
                page.Children.Add(new TextBlock
                {
                    Text = f.Label,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                });
                page.Children.Add(new TextBlock
                {
                    Text = f.Value,
                    Margin = new Thickness(14, -6, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = f.HasResponse ? Brushes.Black : Brushes.DimGray,
                });
                if (!string.IsNullOrWhiteSpace(f.HelpText))
                {
                    page.Children.Add(new TextBlock
                    {
                        Text = f.HelpText,
                        Margin = new Thickness(14, -8, 0, 0),
                        FontSize = 12,
                        Foreground = Brushes.DimGray,
                        TextWrapping = TextWrapping.Wrap,
                    });
                }
                break;

            case TableBlock t:
                page.Children.Add(BuildTablePreview(t));
                break;

            case SignatureBlock s:
                page.Children.Add(new TextBlock
                {
                    Text = "Signatures",
                    FontSize = 16,
                    FontWeight = FontWeight.SemiBold,
                    Margin = new Thickness(0, 12, 0, 0),
                });
                foreach (var sig in s.Signatures)
                {
                    var status = sig.ContentValid ? "verified" : "INVALID";
                    page.Children.Add(new TextBlock
                    {
                        Text = $"[{status}] {sig.Role}: {sig.Signer} - {sig.Scope}",
                        TextWrapping = TextWrapping.Wrap,
                    });
                    page.Children.Add(new TextBlock
                    {
                        Text = $"trust: {sig.Trust} - {sig.Status}",
                        FontSize = 12,
                        Foreground = Brushes.DimGray,
                        TextWrapping = TextWrapping.Wrap,
                    });
                }
                break;
        }
    }

    private static Control BuildTablePreview(TableBlock table)
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 8) };
        var headers = table.ColumnHeaders.Count == 0
            ? "(no columns)"
            : string.Join(" | ", table.ColumnHeaders);
        panel.Children.Add(new TextBlock
        {
            Text = headers,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });

        foreach (var row in table.Rows)
        {
            var values = row.Cells.Select(c => string.IsNullOrWhiteSpace(c.Value) ? "[blank]" : c.Value);
            panel.Children.Add(new TextBlock
            {
                Text = $"{row.Label}: {string.Join(" | ", values)}",
                TextWrapping = TextWrapping.Wrap,
            });
        }

        return panel;
    }

    private static Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }
}
