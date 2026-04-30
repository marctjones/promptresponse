using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PromptResponse.Desktop.Views;

/// <summary>
/// Thin window host for the main UI. All application surface lives in
/// <see cref="MainShellView"/>; this class only owns window chrome (title,
/// icon, persisted position/size).
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
