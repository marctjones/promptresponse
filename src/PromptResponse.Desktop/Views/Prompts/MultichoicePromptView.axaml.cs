using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.Views.Prompts;

public partial class MultichoicePromptView : UserControl
{
    public MultichoicePromptView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SyncCheckboxes();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnChoiceClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || DataContext is not MultichoicePromptViewModel vm) return;
        if (cb.Content is not string value) return;

        if (cb.IsChecked == true) vm.Select(value);
        else vm.Deselect(value);
    }

    private void SyncCheckboxes()
    {
        if (DataContext is not MultichoicePromptViewModel vm) return;
        var host = this.FindControl<ItemsControl>("ChoicesHost");
        if (host == null) return;
        // Initial sync — when the response already contains values, check the matching boxes.
        host.Loaded += (_, _) =>
        {
            foreach (var control in host.GetVisualDescendants())
            {
                if (control is CheckBox cb && cb.Content is string content)
                {
                    cb.IsChecked = vm.IsSelected(content);
                }
            }
        };
    }
}

internal static class VisualTreeHelpers
{
    public static IEnumerable<Avalonia.Visual> GetVisualDescendants(this Avalonia.Controls.Control root)
    {
        foreach (var child in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root))
        {
            yield return child;
        }
    }
}
