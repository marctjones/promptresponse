using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.Views.Editor;

namespace PromptResponse.Desktop.Views;

public partial class MainShellView : UserControl
{
    public MainShellView()
    {
        InitializeComponent();
        WireTopLevelSectionDragDrop();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

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
