using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PromptResponse.Desktop.ViewModels;

namespace PromptResponse.Desktop.Views;

public partial class DisplayPreferencesView : UserControl
{
    public DisplayPreferencesView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnResetClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DisplayPreferencesViewModel vm)
        {
            vm.Reset();
        }
    }
}
