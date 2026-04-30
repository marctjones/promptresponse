using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace PromptResponse.Desktop.Views;

public partial class KeyboardShortcutsDialog : Window
{
    public KeyboardShortcutsDialog() { InitializeComponent(); }
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
