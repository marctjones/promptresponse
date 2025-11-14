using Avalonia.Controls;
using PromptResponse.Desktop.ViewModels;

namespace PromptResponse.Desktop.Views;

/// <summary>
/// Window for managing digital certificates
/// </summary>
public partial class CertificateManagementWindow : Window
{
    public CertificateManagementWindow()
    {
        InitializeComponent();
    }

    public CertificateManagementWindow(CertificateManagementViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
