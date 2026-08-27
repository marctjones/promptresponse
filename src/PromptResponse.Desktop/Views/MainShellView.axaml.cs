using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views.Editor;

namespace PromptResponse.Desktop.Views;

public partial class MainShellView : UserControl
{
    private MainShellViewModel? _subscribedShell;

    public MainShellView()
    {
        InitializeComponent();
        WireTopLevelSectionDragDrop();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Re-wire the "click an advisory → focus that field" bridge. The view
        // owns the visual tree, so it does the scroll + focus; the VM just
        // raises the request.
        if (_subscribedShell != null)
        {
            _subscribedShell.FocusPromptRequested -= OnFocusPromptRequested;
            _subscribedShell = null;
        }
        if (DataContext is MainShellViewModel shell)
        {
            shell.FocusPromptRequested += OnFocusPromptRequested;
            _subscribedShell = shell;
        }
    }

    private void OnFocusPromptRequested(string promptId)
    {
        // Find the realized prompt control whose VM has this id, bring it into
        // view, and focus its first focusable input. Best-effort: if the control
        // isn't realized (e.g. scrolled far off in a virtualized list), no-op.
        var target = this.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(c => c.DataContext is PromptViewModelBase p && p.Id == promptId);
        if (target == null)
        {
            return;
        }

        target.BringIntoView();
        var focusable = target.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Focusable)
            ?? target;
        focusable.Focus();
    }

    /// <summary>Register the top-level edit-mode sections list as a drop target
    /// for "section" drags. A section dragged inside its own (top-level) list
    /// gets reordered there; a section dragged from a nested list whose
    /// payload doesn't appear in this list is silently ignored by the helper's
    /// IndexOf check, naturally scoping reorder to same-parent.</summary>
    private void WireTopLevelSectionDragDrop()
    {
        var sectionsList = this.FindControl<ItemsControl>("SectionsHostEdit");
        if (sectionsList != null)
        {
            DragReorderBehavior.RegisterDropTarget(sectionsList, "section", (from, to) =>
            {
                if (DataContext is MainShellViewModel shell) shell.MoveTopLevelSection(from, to);
            });
        }
    }

    private Window? OwnerWindow =>
        Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

    /// <summary>File &gt; Exit.</summary>
    /// <remarks>
    /// Closes the window rather than calling Shutdown so the window's own Closing
    /// handling still runs; shutting down directly would skip it.
    /// </remarks>
    private void OnExitClicked(object? sender, RoutedEventArgs e) =>
        (TopLevel.GetTopLevel(this) as Window)?.Close();

    private void OnDisplayPreferencesClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainShellViewModel shell) return;
        var prefs = new DisplayPreferencesView
        {
            DataContext = new DisplayPreferencesViewModel(shell.ProfileService),
        };
        var window = new Window
        {
            Title = "Display Preferences",
            Width = 560,
            Height = 640,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = prefs,
        };
        if (OwnerWindow is { } owner) window.ShowDialog(owner);
        else window.Show();
    }

    private void OnKeyboardShortcutsClicked(object? sender, RoutedEventArgs e)
    {
        var dialog = new KeyboardShortcutsDialog();
        if (OwnerWindow is { } owner) dialog.ShowDialog(owner);
        else dialog.Show();
    }

    private void OnRefreshAdvisoriesClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainShellViewModel shell) shell.RefreshAdvisories();
    }

    private void OnAboutClicked(object? sender, RoutedEventArgs e)
    {
        var about = new AboutDialog();
        if (OwnerWindow is { } owner) about.ShowDialog(owner);
        else about.Show();
    }
}
