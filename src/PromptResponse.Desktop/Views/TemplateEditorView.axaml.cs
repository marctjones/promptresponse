using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PromptResponse.Desktop.ViewModels;
using System;
using System.Linq;

namespace PromptResponse.Desktop.Views;

public partial class TemplateEditorView : UserControl
{
    private readonly ILogger<TemplateEditorView>? _logger;

    public TemplateEditorView()
    {
        InitializeComponent();

        // Get logger from service provider (may be null in design mode)
        _logger = App.ServiceProvider?.GetService<ILogger<TemplateEditorView>>();

        if (_logger != null)
        {
            _logger.LogInformation("=== TemplateEditorView Layout Diagnostics ===");

            // Log initial state after layout is loaded
            this.AttachedToVisualTree += (s, e) =>
            {
                LogLayoutHierarchy("AttachedToVisualTree");
            };

            // Track size changes
            this.SizeChanged += (s, e) =>
            {
                _logger.LogDebug("TemplateEditorView SizeChanged: {OldSize} -> {NewSize}",
                    e.PreviousSize, e.NewSize);
                LogLayoutHierarchy("SizeChanged");
            };
        }
    }

    private void LogLayoutHierarchy(string eventName)
    {
        if (_logger == null) return;

        try
        {
            _logger.LogInformation("--- Layout Hierarchy ({Event}) ---", eventName);

            // UserControl (this)
            _logger.LogInformation("TemplateEditorView (UserControl):");
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
        if (sender is not Button button || button.Tag is not EditableSectionViewModel targetSection)
        {
            _logger?.LogWarning("OnSectionNavigationClick: sender is not a Button or Tag is not EditableSectionViewModel");
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
                .Where(b => b.Tag is EditableSectionViewModel)
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
}
