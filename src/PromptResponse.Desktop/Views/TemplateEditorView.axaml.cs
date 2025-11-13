using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

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
}
