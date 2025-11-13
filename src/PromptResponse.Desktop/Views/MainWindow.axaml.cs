using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace PromptResponse.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        Console.WriteLine("[MainWindow] Constructor called");
        Console.WriteLine("[MainWindow] Initializing components...");
        InitializeComponent();
        Console.WriteLine("[MainWindow] Components initialized");

        // Hook window events
        Opened += (s, e) => Console.WriteLine("[MainWindow] Window Opened event fired");
        Closing += (s, e) => Console.WriteLine("[MainWindow] Window Closing event fired");
        Closed += (s, e) => Console.WriteLine("[MainWindow] Window Closed event fired");

        Console.WriteLine("[MainWindow] Constructor complete");
    }

    private void OnExitClicked(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("[MainWindow] Exit menu item clicked");
        Close();
    }

    private async void OnAboutClicked(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("[MainWindow] About menu item clicked");

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

        Console.WriteLine("[MainWindow] Showing About dialog");
        await about.ShowDialog(this);
        Console.WriteLine("[MainWindow] About dialog closed");
    }
}
