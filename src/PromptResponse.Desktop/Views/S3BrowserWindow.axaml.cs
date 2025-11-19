using Avalonia.Controls;
using PromptResponse.Desktop.ViewModels;

namespace PromptResponse.Desktop.Views;

/// <summary>
/// Window for browsing and downloading APR documents from S3 storage.
/// </summary>
public partial class S3BrowserWindow : Window
{
    public S3BrowserWindow()
    {
        InitializeComponent();
    }

    public S3BrowserWindow(S3BrowserViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
