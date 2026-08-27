using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;

namespace PromptResponse.Desktop.Views;

/// <summary>
/// A small dialog for making a self-signed signing key.
/// </summary>
/// <remarks>
/// Creation only, on purpose. Storing, rotating, renewing and revoking key material is
/// the platform's job and this application does not pretend otherwise; what it offers is
/// a way to try signing without first learning openssl.
/// </remarks>
public partial class CreateSigningKeyDialog : Window
{
    private readonly IFileService? _files;

    public CreateSigningKeyDialog() : this(null) { }

    public CreateSigningKeyDialog(IFileService? files)
    {
        _files = files;
        InitializeComponent();
        DataContext ??= new CreateSigningKeyViewModel();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>What was created, for a caller that wants to report it.</summary>
    public CreatedSigningKey? Created { get; private set; }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private async void OnCreate(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CreateSigningKeyViewModel vm || !vm.CanCreate) return;

        var path = _files is null
            ? null
            : await _files.PickExportPathAsync(vm.SuggestedFileName,
                "Save signing key", "Signing key", "pfx");
        if (string.IsNullOrWhiteSpace(path)) return;

        var result = this.FindControl<SelectableTextBlock>("ResultText");
        try
        {
            Created = vm.Create(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                      or System.Security.Cryptography.CryptographicException)
        {
            // Reported in the dialog rather than thrown at the app: a key that could not
            // be written is a thing to tell somebody, not a crash.
            if (result is not null)
            {
                result.Text = $"Could not create the key: {ex.Message}";
                result.IsVisible = true;
            }
            return;
        }

        if (result is not null)
        {
            var shared = Created.PublicCertificatePath is null
                ? "No public certificate was written, so nobody else can verify these signatures yet."
                : $"Share {Path.GetFileName(Created.PublicCertificatePath)} with anyone who needs to verify your signatures.";

            result.Text =
                $"Saved {Path.GetFileName(Created.PrivateKeyPath)}.\n" +
                $"Thumbprint {Created.Thumbprint}\n" +
                $"Usable until {Created.Expires:yyyy-MM-dd}. {shared}";
            result.IsVisible = true;
        }

        this.FindControl<Button>("CreateButton")!.IsEnabled = false;
    }
}
