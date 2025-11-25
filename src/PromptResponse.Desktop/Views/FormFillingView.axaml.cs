using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PromptResponse.Desktop.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PromptResponse.Desktop.Views;

public partial class FormFillingView : UserControl
{
    private readonly ILogger<FormFillingView>? _logger;

    public FormFillingView()
    {
        InitializeComponent();

        // Get logger from service provider (may be null in design mode)
        _logger = App.ServiceProvider?.GetService<ILogger<FormFillingView>>();

        if (_logger != null)
        {
            _logger.LogInformation("=== FormFillingView Layout Diagnostics ===");

            // Log initial state after layout is loaded
            this.AttachedToVisualTree += (s, e) =>
            {
                LogLayoutHierarchy("AttachedToVisualTree");
            };

            // Track size changes
            this.SizeChanged += (s, e) =>
            {
                _logger.LogDebug("FormFillingView SizeChanged: {OldSize} -> {NewSize}",
                    e.PreviousSize, e.NewSize);
                LogLayoutHierarchy("SizeChanged");
            };
        }

        // Subscribe to DataContext changes to set up navigation event handler
        this.DataContextChanged += OnDataContextChanged;

        // Handle Enter key in search box
        var searchTextBox = this.FindControl<TextBox>("SearchTextBox");
        if (searchTextBox != null)
        {
            searchTextBox.KeyDown += OnSearchTextBoxKeyDown;
        }
    }

    /// <summary>
    /// Handles DataContext changes to subscribe to navigation events.
    /// </summary>
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is FormFillingViewModel viewModel)
        {
            viewModel.OnNavigateToPromptRequested += NavigateToPrompt;
            _logger?.LogInformation("Subscribed to OnNavigateToPromptRequested event");
        }
    }

    /// <summary>
    /// Handles Enter key press in search box to go to next match.
    /// </summary>
    private void OnSearchTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is FormFillingViewModel viewModel)
        {
            if (viewModel.NextMatchCommand.CanExecute(null))
            {
                viewModel.NextMatchCommand.Execute(null);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (DataContext is FormFillingViewModel vm)
            {
                vm.ClearSearchCommand.Execute(null);
            }
            e.Handled = true;
        }
    }

    /// <summary>
    /// Navigates to a specific prompt by scrolling it into view.
    /// </summary>
    private void NavigateToPrompt(PromptViewModel targetPrompt)
    {
        _logger?.LogInformation("Navigating to prompt: {Label}", targetPrompt.Label);

        try
        {
            var scrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
            if (scrollViewer == null)
            {
                _logger?.LogError("Could not find MainScrollViewer");
                return;
            }

            // Find all StackPanels that are prompt containers (they have a TextBlock with matching label)
            var allTextBlocks = scrollViewer.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(tb => tb.Text == targetPrompt.Label && tb.Classes.Contains("field-label"))
                .ToList();

            _logger?.LogDebug("Found {Count} TextBlocks with label: {Label}", allTextBlocks.Count, targetPrompt.Label);

            if (allTextBlocks.Count > 0)
            {
                // Find the parent StackPanel of the first matching TextBlock
                var targetTextBlock = allTextBlocks.First();
                var parentStackPanel = targetTextBlock.Parent as StackPanel;

                if (parentStackPanel != null)
                {
                    // Expand any collapsed parent sections
                    var parentExpander = parentStackPanel.FindLogicalAncestorOfType<Expander>();
                    while (parentExpander != null)
                    {
                        if (!parentExpander.IsExpanded)
                        {
                            parentExpander.IsExpanded = true;
                        }
                        parentExpander = parentExpander.FindLogicalAncestorOfType<Expander>();
                    }

                    // Bring the prompt into view
                    parentStackPanel.BringIntoView();
                    _logger?.LogInformation("Prompt scrolled into view successfully");
                }
            }
            else
            {
                _logger?.LogWarning("Could not find TextBlock for prompt: {Label}", targetPrompt.Label);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error navigating to prompt: {Label}", targetPrompt.Label);
        }
    }

    private void LogLayoutHierarchy(string eventName)
    {
        if (_logger == null) return;

        try
        {
            _logger.LogInformation("--- Layout Hierarchy ({Event}) ---", eventName);

            // UserControl (this)
            _logger.LogInformation("FormFillingView (UserControl):");
            _logger.LogInformation("  Bounds: {Bounds}", this.Bounds);
            _logger.LogInformation("  Width: {Width}", this.Width);
            _logger.LogInformation("  DesiredSize: {DesiredSize}", this.DesiredSize);
            _logger.LogInformation("  HorizontalAlignment: {HAlign}", this.HorizontalAlignment);

            // Try to find DockPanel
            if (this.Content is DockPanel dockPanel)
            {
                _logger.LogInformation("DockPanel:");
                _logger.LogInformation("  Bounds: {Bounds}", dockPanel.Bounds);
                _logger.LogInformation("  Width: {Width}", dockPanel.Width);
                _logger.LogInformation("  HorizontalAlignment: {HAlign}", dockPanel.HorizontalAlignment);

                // Find ScrollViewer
                foreach (var child in dockPanel.Children)
                {
                    if (child is ScrollViewer scrollViewer)
                    {
                        _logger.LogInformation("ScrollViewer:");
                        _logger.LogInformation("  Bounds: {Bounds}", scrollViewer.Bounds);
                        _logger.LogInformation("  Width: {Width}", scrollViewer.Width);
                        _logger.LogInformation("  HorizontalAlignment: {HAlign}", scrollViewer.HorizontalAlignment);

                        // Find Grid
                        if (scrollViewer.Content is Grid grid)
                        {
                            _logger.LogInformation("Grid:");
                            _logger.LogInformation("  Bounds: {Bounds}", grid.Bounds);
                            _logger.LogInformation("  Width: {Width}", grid.Width);
                            _logger.LogInformation("  HorizontalAlignment: {HAlign}", grid.HorizontalAlignment);

                            // Find StackPanel
                            foreach (var gridChild in grid.Children)
                            {
                                if (gridChild is StackPanel stackPanel)
                                {
                                    _logger.LogInformation("StackPanel:");
                                    _logger.LogInformation("  Bounds: {Bounds}", stackPanel.Bounds);
                                    _logger.LogInformation("  Width: {Width}", stackPanel.Width);
                                    _logger.LogInformation("  HorizontalAlignment: {HAlign}", stackPanel.HorizontalAlignment);
                                    _logger.LogInformation("  Children count: {Count}", stackPanel.Children.Count);
                                }
                            }
                        }
                    }
                }
            }

            _logger.LogInformation("--- End Layout Hierarchy ---");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging layout hierarchy");
        }
    }

    /// <summary>
    /// Handles click on section navigation button in sidebar.
    /// Scrolls the main content area to bring the selected section into view.
    /// </summary>
    private void OnSectionNavigationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not SectionViewModel targetSection)
        {
            _logger?.LogWarning("OnSectionNavigationClick: sender is not a Button or Tag is not SectionViewModel");
            return;
        }

        _logger?.LogInformation("Section navigation clicked: {SectionTitle}", targetSection.Title);

        try
        {
            // Find the main ScrollViewer
            var scrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
            if (scrollViewer == null)
            {
                _logger?.LogError("Could not find MainScrollViewer");
                return;
            }

            // Find all Border elements with Tags (these are section containers)
            var allBorders = scrollViewer.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.Tag is SectionViewModel)
                .ToList();

            _logger?.LogDebug("Found {Count} section borders", allBorders.Count);

            // Find the border that contains our target section
            var targetBorder = allBorders.FirstOrDefault(b => b.Tag == targetSection);

            if (targetBorder != null)
            {
                _logger?.LogInformation("Found target border, bringing into view");

                // Expand the section if it's collapsed
                var expander = targetBorder.GetVisualDescendants().OfType<Expander>().FirstOrDefault();
                if (expander != null && !expander.IsExpanded)
                {
                    targetSection.IsExpanded = true;
                    _logger?.LogDebug("Expanded section");
                }

                // Bring the border into view (this scrolls it)
                targetBorder.BringIntoView();

                _logger?.LogInformation("Section scrolled into view successfully");
            }
            else
            {
                _logger?.LogWarning("Could not find Border for section: {Title}", targetSection.Title);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error scrolling to section: {Title}", targetSection.Title);
        }
    }

    /// <summary>
    /// Handles click on calendar button for date fields.
    /// Opens a calendar picker that allows selecting a date while still allowing freeform text entry.
    /// </summary>
    private void OnDatePickerClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not PromptViewModel promptViewModel)
        {
            _logger?.LogWarning("OnDatePickerClick: sender is not a Button or Tag is not PromptViewModel");
            return;
        }

        _logger?.LogInformation("Date picker clicked for prompt: {Label}", promptViewModel.Label);

        try
        {
            // Create a CalendarDatePicker
            var datePicker = new CalendarDatePicker
            {
                MinWidth = 250
            };

            // Try to parse existing response as a date
            if (!string.IsNullOrWhiteSpace(promptViewModel.Response))
            {
                if (DateTime.TryParse(promptViewModel.Response, out var existingDate))
                {
                    datePicker.SelectedDate = existingDate;
                }
            }

            // Create a flyout to show the date picker (declare before initializer to avoid capture issues)
            Flyout? flyout = null;
            flyout = new Flyout
            {
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        datePicker,
                        new StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            Spacing = 8,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Children =
                            {
                                new Button
                                {
                                    Content = "OK",
                                    Classes = { "accent" },
                                    Command = new RelayCommand(() =>
                                    {
                                        if (datePicker.SelectedDate.HasValue)
                                        {
                                            var date = datePicker.SelectedDate.Value;
                                            promptViewModel.Response = date.ToString("yyyy-MM-dd");
                                            _logger?.LogInformation("Date selected: {Date}", promptViewModel.Response);
                                        }
                                        flyout?.Hide();
                                    })
                                },
                                new Button
                                {
                                    Content = "Cancel",
                                    Command = new RelayCommand(() => flyout?.Hide())
                                }
                            }
                        }
                    }
                }
            };

            flyout.ShowAt(button);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error showing date picker for prompt: {Label}", promptViewModel.Label);
        }
    }

    /// <summary>
    /// Handles click on toggle button to switch between smart controls and plain text.
    /// </summary>
    private void OnToggleSmartControlClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not PromptViewModel promptViewModel)
        {
            _logger?.LogWarning("OnToggleSmartControlClick: sender is not a Button or Tag is not PromptViewModel");
            return;
        }

        _logger?.LogInformation("Toggle smart control clicked for prompt: {Label}, current state: {UseSmartControl}",
            promptViewModel.Label, promptViewModel.UseSmartControl);

        promptViewModel.UseSmartControl = !promptViewModel.UseSmartControl;

        _logger?.LogInformation("Smart control toggled to: {UseSmartControl}", promptViewModel.UseSmartControl);
    }

    /// <summary>
    /// Handles radio button click for single-choice fields.
    /// </summary>
    private void OnRadioButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton radioButton || radioButton.Tag is not string selectedValue)
        {
            _logger?.LogWarning("OnRadioButtonClick: sender is not a RadioButton or Tag is not string");
            return;
        }

        // Find the PromptViewModel from the parent ItemsControl
        var itemsControl = radioButton.FindLogicalAncestorOfType<ItemsControl>();
        if (itemsControl?.Tag is PromptViewModel promptViewModel)
        {
            promptViewModel.Response = selectedValue;
            _logger?.LogInformation("Radio button selected: {Value} for prompt: {Label}", selectedValue, promptViewModel.Label);
        }
    }

    /// <summary>
    /// Handles checkbox click for boolean fields.
    /// </summary>
    private void OnBooleanCheckBoxClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox || checkBox.Tag is not PromptViewModel promptViewModel)
        {
            _logger?.LogWarning("OnBooleanCheckBoxClick: sender is not a CheckBox or Tag is not PromptViewModel");
            return;
        }

        // Store as "true" or "false" string
        promptViewModel.Response = checkBox.IsChecked == true ? "true" : "false";
        _logger?.LogInformation("Boolean checkbox toggled: {Value} for prompt: {Label}", promptViewModel.Response, promptViewModel.Label);
    }

    /// <summary>
    /// Handles checkbox click for multichoice fields.
    /// </summary>
    private void OnMultiChoiceCheckBoxClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox || checkBox.Tag is not string value)
        {
            _logger?.LogWarning("OnMultiChoiceCheckBoxClick: sender is not a CheckBox or Tag is not string");
            return;
        }

        // Find the PromptViewModel from the parent ItemsControl
        var itemsControl = checkBox.FindLogicalAncestorOfType<ItemsControl>();
        if (itemsControl?.Tag is PromptViewModel promptViewModel)
        {
            // Get current response as comma-separated list
            var current = string.IsNullOrWhiteSpace(promptViewModel.Response)
                ? new List<string>()
                : promptViewModel.Response.Split(',').Select(s => s.Trim()).ToList();

            if (checkBox.IsChecked == true)
            {
                if (!current.Contains(value))
                {
                    current.Add(value);
                }
            }
            else
            {
                current.Remove(value);
            }

            promptViewModel.Response = string.Join(", ", current);
            _logger?.LogInformation("Multichoice updated: {Values} for prompt: {Label}", promptViewModel.Response, promptViewModel.Label);
        }
    }

    /// <summary>
    /// Handles click on Add Row button for dynamic tables.
    /// </summary>
    private void OnAddRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not PromptViewModel promptViewModel)
        {
            _logger?.LogWarning("OnAddRowClick: sender is not a Button or Tag is not PromptViewModel");
            return;
        }

        _logger?.LogInformation("Add row clicked for table: {Label}", promptViewModel.Label);
        promptViewModel.AddRow();
    }

    /// <summary>
    /// Handles click on Remove Row button for dynamic tables.
    /// </summary>
    private void OnRemoveRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not PromptViewModel promptViewModel)
        {
            _logger?.LogWarning("OnRemoveRowClick: sender is not a Button or Tag is not PromptViewModel");
            return;
        }

        _logger?.LogInformation("Remove row clicked for table: {Label}", promptViewModel.Label);
        promptViewModel.RemoveRow();
    }

    /// <summary>
    /// Handles click on password visibility toggle button.
    /// </summary>
    private void OnTogglePasswordVisibility(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not PromptViewModel promptViewModel)
        {
            _logger?.LogWarning("OnTogglePasswordVisibility: sender is not a Button or Tag is not PromptViewModel");
            return;
        }

        promptViewModel.ShowPassword = !promptViewModel.ShowPassword;
        _logger?.LogInformation("Password visibility toggled: {ShowPassword} for prompt: {Label}",
            promptViewModel.ShowPassword, promptViewModel.Label);
    }

    /// <summary>
    /// Handles click on file browse button.
    /// </summary>
    private async void OnBrowseFileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not PromptViewModel promptViewModel)
        {
            _logger?.LogWarning("OnBrowseFileClick: sender is not a Button or Tag is not PromptViewModel");
            return;
        }

        _logger?.LogInformation("Browse file clicked for prompt: {Label}", promptViewModel.Label);

        try
        {
            // Get the top-level window
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                _logger?.LogError("Could not find top-level window");
                return;
            }

            // Show file picker
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = $"Select file for: {promptViewModel.Label}",
                AllowMultiple = false
            });

            if (files.Count > 0)
            {
                var file = files[0];
                promptViewModel.Response = file.Path.LocalPath;
                _logger?.LogInformation("File selected: {Path} for prompt: {Label}", file.Path.LocalPath, promptViewModel.Label);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error browsing for file for prompt: {Label}", promptViewModel.Label);
        }
    }

    /// <summary>
    /// Handles time picker selection changed.
    /// </summary>
    private void OnTimePickerChanged(object? sender, TimePickerSelectedValueChangedEventArgs e)
    {
        if (sender is not TimePicker timePicker)
        {
            return;
        }

        // Find the PromptViewModel - the TimePicker's Tag should be the prompt
        if (timePicker.Tag is PromptViewModel promptViewModel && e.NewTime.HasValue)
        {
            promptViewModel.Response = e.NewTime.Value.ToString(@"hh\:mm\:ss");
            _logger?.LogInformation("Time selected: {Time} for prompt: {Label}", promptViewModel.Response, promptViewModel.Label);
        }
    }

    /// <summary>
    /// Handles date picker selection changed for datetime fields.
    /// </summary>
    private void OnDateTimePickerChanged(object? sender, DatePickerSelectedValueChangedEventArgs e)
    {
        if (sender is not CalendarDatePicker datePicker)
        {
            return;
        }

        // Find the PromptViewModel from the parent container
        var parent = datePicker.Parent;
        while (parent != null && parent is not Grid grid)
        {
            parent = parent.Parent as Control;
        }

        if (parent is Grid containerGrid && containerGrid.Tag is PromptViewModel promptViewModel && e.NewDate.HasValue)
        {
            // Get existing time portion if any
            var existingTime = TimeSpan.Zero;
            if (!string.IsNullOrEmpty(promptViewModel.Response) && DateTime.TryParse(promptViewModel.Response, out var existingDt))
            {
                existingTime = existingDt.TimeOfDay;
            }

            var newDateTime = e.NewDate.Value.Date.Add(existingTime);
            promptViewModel.Response = newDateTime.ToString("yyyy-MM-ddTHH:mm:ss");
            _logger?.LogInformation("DateTime date portion updated: {DateTime} for prompt: {Label}", promptViewModel.Response, promptViewModel.Label);
        }
    }

    /// <summary>
    /// Handles click on color picker button.
    /// </summary>
    private void OnColorPickerClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not PromptViewModel promptViewModel)
        {
            _logger?.LogWarning("OnColorPickerClick: sender is not a Button or Tag is not PromptViewModel");
            return;
        }

        _logger?.LogInformation("Color picker clicked for prompt: {Label}", promptViewModel.Label);

        // Common color palette
        var colors = new[]
        {
            "#FF0000", "#FF6600", "#FFCC00", "#33CC00", "#00CCCC",
            "#0066FF", "#6600FF", "#CC00CC", "#000000", "#666666",
            "#CCCCCC", "#FFFFFF", "#8B4513", "#FFB6C1", "#20B2AA"
        };

        var colorGrid = new UniformGrid
        {
            Columns = 5,
            Width = 150,
            Height = 90
        };

        Flyout? flyout = null;
        foreach (var colorHex in colors)
        {
            var colorButton = new Button
            {
                Width = 28,
                Height = 28,
                Margin = new Avalonia.Thickness(1),
                Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(colorHex)),
                Tag = colorHex
            };
            colorButton.Click += (s, args) =>
            {
                if (s is Button btn && btn.Tag is string hex)
                {
                    promptViewModel.Response = hex;
                    _logger?.LogInformation("Color selected: {Color} for prompt: {Label}", hex, promptViewModel.Label);
                    flyout?.Hide();
                }
            };
            colorGrid.Children.Add(colorButton);
        }

        flyout = new Flyout
        {
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    colorGrid,
                    new TextBox
                    {
                        Text = promptViewModel.Response,
                        Watermark = "#RRGGBB or color name",
                        Width = 150
                    }
                }
            }
        };

        flyout.ShowAt(button);
    }

    /// <summary>
    /// Handles signature clear button click.
    /// </summary>
    private void OnClearSignatureClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not PromptViewModel promptViewModel)
        {
            _logger?.LogWarning("OnClearSignatureClick: sender is not a Button or Tag is not PromptViewModel");
            return;
        }

        promptViewModel.Response = "";
        _logger?.LogInformation("Signature cleared for prompt: {Label}", promptViewModel.Label);
    }
}
