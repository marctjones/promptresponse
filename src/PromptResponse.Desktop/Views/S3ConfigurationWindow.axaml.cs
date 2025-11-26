using Avalonia.Controls;
using PromptResponse.Desktop.ViewModels;

namespace PromptResponse.Desktop.Views;

public partial class S3ConfigurationWindow : Window
{
    public S3ConfigurationWindow()
    {
        InitializeComponent();
    }

    public S3ConfigurationWindow(S3ConfigurationViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
