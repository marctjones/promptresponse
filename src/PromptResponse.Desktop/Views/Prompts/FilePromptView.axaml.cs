using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.Views.Prompts;

public partial class FilePromptView : UserControl
{
    public FilePromptView() { InitializeComponent(); }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private async void OnBrowseClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not FilePromptViewModel vm) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { CanOpen: true } storage) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose file for " + vm.Label,
            AllowMultiple = false,
        });
        if (files.Count > 0)
        {
            vm.Response = files[0].Path.LocalPath;
        }
    }
}
