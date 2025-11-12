using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PromptResponse.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnExitClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnAboutClicked(object? sender, RoutedEventArgs e)
    {
        var about = new Window
        {
            Title = "About PromptResponse",
            Width = 400,
            Height = 300,
            Content = new TextBlock
            {
                Text = "PromptResponse\n\nA flexible form creation and filling application.\n\nVersion 0.1.0\n\nGPL-3.0 License",
                TextAlignment = Avalonia.Media.TextAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(20)
            }
        };

        await about.ShowDialog(this);
    }
}
