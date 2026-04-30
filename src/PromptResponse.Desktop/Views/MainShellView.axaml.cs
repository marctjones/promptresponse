using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PromptResponse.Desktop.ViewModels;

namespace PromptResponse.Desktop.Views;

public partial class MainShellView : UserControl
{
    public MainShellView() { InitializeComponent(); }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

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

    private void OnAboutClicked(object? sender, RoutedEventArgs e)
    {
        var about = new Window
        {
            Title = "About PromptResponse",
            Width = 420,
            Height = 240,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new TextBlock
            {
                Text = "PromptResponse\n\nA semantic, accessible form designer and filler.\n\n0.1 baseline · GPL-3.0",
                TextAlignment = Avalonia.Media.TextAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(24),
            },
        };
        if (OwnerWindow is { } owner) about.ShowDialog(owner);
        else about.Show();
    }
}
