using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using PromptResponse.Core.Models;
using PromptResponse.Core.Services.Certificates;
using X509CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// ViewModel for certificate management UI
/// </summary>
public class CertificateManagementViewModel : ViewModelBase
{
    private readonly ICertificateGenerator _certificateGenerator;
    private readonly ICertificateStore _certificateStore;
    private readonly IOnePasswordService _onePasswordService;
    private readonly ILogger<CertificateManagementViewModel> _logger;

    private string _commonName = string.Empty;
    private string _email = string.Empty;
    private string _organization = string.Empty;
    private bool _emailSigningEnabled = true;
    private bool _documentSigningEnabled = true;
    private bool _codeSigningEnabled = false;
    private int _validityDays = 365;
    private string _statusMessage = string.Empty;
    private bool _isGenerating = false;
    private bool _is1PasswordAvailable = false;

    public CertificateManagementViewModel(
        ICertificateGenerator certificateGenerator,
        ICertificateStore certificateStore,
        IOnePasswordService onePasswordService,
        ILogger<CertificateManagementViewModel> logger)
    {
        _certificateGenerator = certificateGenerator ?? throw new ArgumentNullException(nameof(certificateGenerator));
        _certificateStore = certificateStore ?? throw new ArgumentNullException(nameof(certificateStore));
        _onePasswordService = onePasswordService ?? throw new ArgumentNullException(nameof(onePasswordService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        GenerateCertificateCommand = new RelayCommand(
            async () => await GenerateCertificateAsync(),
            () => !string.IsNullOrWhiteSpace(CommonName) && !string.IsNullOrWhiteSpace(Email) && !IsGenerating);

        InstallCertificateCommand = new RelayCommand(
            async (param) => await InstallCertificateAsync((X509Certificate2)param!),
            () => !IsGenerating);

        ExportCertificateCommand = new RelayCommand(
            async (param) => await ExportCertificateAsync((X509Certificate2)param!));

        RefreshInstalledCertificatesCommand = new RelayCommand(
            () => { RefreshInstalledCertificates(); return Task.CompletedTask; });

        SaveTo1PasswordCommand = new RelayCommand(
            async (param) => await SaveTo1PasswordAsync((X509Certificate2)param!),
            () => Is1PasswordAvailable);

        Check1PasswordCommand = new RelayCommand(
            async () => await Check1PasswordAvailabilityAsync());

        // Load installed certificates on startup
        RefreshInstalledCertificates();

        // Check 1Password availability
        _ = Check1PasswordAvailabilityAsync();
    }

    #region Properties

    public string CommonName
    {
        get => _commonName;
        set
        {
            if (SetProperty(ref _commonName, value))
            {
                (GenerateCertificateCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string Email
    {
        get => _email;
        set
        {
            if (SetProperty(ref _email, value))
            {
                (GenerateCertificateCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string Organization
    {
        get => _organization;
        set => SetProperty(ref _organization, value);
    }

    public bool EmailSigningEnabled
    {
        get => _emailSigningEnabled;
        set => SetProperty(ref _emailSigningEnabled, value);
    }

    public bool DocumentSigningEnabled
    {
        get => _documentSigningEnabled;
        set => SetProperty(ref _documentSigningEnabled, value);
    }

    public bool CodeSigningEnabled
    {
        get => _codeSigningEnabled;
        set => SetProperty(ref _codeSigningEnabled, value);
    }

    public int ValidityDays
    {
        get => _validityDays;
        set => SetProperty(ref _validityDays, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsGenerating
    {
        get => _isGenerating;
        set
        {
            if (SetProperty(ref _isGenerating, value))
            {
                (GenerateCertificateCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool Is1PasswordAvailable
    {
        get => _is1PasswordAvailable;
        set
        {
            if (SetProperty(ref _is1PasswordAvailable, value))
            {
                (SaveTo1PasswordCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public ObservableCollection<CertificateViewModel> GeneratedCertificates { get; } = new();
    public ObservableCollection<CertificateViewModel> InstalledCertificates { get; } = new();

    #endregion

    #region Commands

    public ICommand GenerateCertificateCommand { get; }
    public ICommand InstallCertificateCommand { get; }
    public ICommand ExportCertificateCommand { get; }
    public ICommand RefreshInstalledCertificatesCommand { get; }
    public ICommand SaveTo1PasswordCommand { get; }
    public ICommand Check1PasswordCommand { get; }

    #endregion

    #region Methods

    private async Task GenerateCertificateAsync()
    {
        IsGenerating = true;
        StatusMessage = "Generating certificate...";

        try
        {
            _logger.LogInformation("Generating certificate for {CommonName} ({Email})", CommonName, Email);

            var usage = CertificateUsage.All & 0; // Start with none
            if (EmailSigningEnabled) usage |= CertificateUsage.EmailSigning;
            if (DocumentSigningEnabled) usage |= CertificateUsage.DocumentSigning;
            if (CodeSigningEnabled) usage |= CertificateUsage.CodeSigning;

            var request = new Core.Models.CertificateRequest
            {
                CommonName = CommonName,
                Email = Email,
                Organization = Organization,
                ValidityDays = ValidityDays,
                Usage = usage,
                KeySize = 2048
            };

            await Task.Run(() =>
            {
                var cert = _certificateGenerator.GenerateSelfSignedCertificate(request);
                var certViewModel = new CertificateViewModel(cert);

                // Update UI on main thread
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    GeneratedCertificates.Add(certViewModel);
                });
            });

            StatusMessage = $"✅ Certificate generated successfully for {CommonName}";
            _logger.LogInformation("Certificate generated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating certificate");
            StatusMessage = $"❌ Error: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private async Task InstallCertificateAsync(X509Certificate2? cert)
    {
        if (cert == null) return;

        IsGenerating = true;
        StatusMessage = "Installing certificate...";

        try
        {
            _logger.LogInformation("Installing certificate {Subject}", cert.Subject);

            var success = await _certificateStore.InstallCertificateAsync(
                cert,
                StoreLocation.CurrentUser,
                StoreName.My);

            if (success)
            {
                StatusMessage = $"✅ Certificate installed to system store";
                _logger.LogInformation("Certificate installed successfully");

                // Refresh installed certificates list
                RefreshInstalledCertificates();
            }
            else
            {
                StatusMessage = "❌ Failed to install certificate (check permissions)";
                _logger.LogWarning("Certificate installation failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error installing certificate");
            StatusMessage = $"❌ Error: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private async Task ExportCertificateAsync(X509Certificate2? cert)
    {
        if (cert == null) return;

        try
        {
            _logger.LogInformation("Exporting certificate {Subject}", cert.Subject);

            // For now, just export to Downloads folder
            // TODO: Add file picker dialog
            var downloadsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads");

            var fileName = $"{SanitizeFileName(CommonName)}_{DateTime.Now:yyyyMMdd_HHmmss}.pfx";
            var filePath = Path.Combine(downloadsPath, fileName);

            var password = $"PromptResponse{DateTime.Now:yyyyMMddHHmmss}!";

            await Task.Run(() =>
            {
                var pfxData = _certificateGenerator.ExportPfx(cert, password);
                File.WriteAllBytes(filePath, pfxData);
            });

            StatusMessage = $"✅ Certificate exported to: {filePath}\n🔑 Password: {password}";
            _logger.LogInformation("Certificate exported to {Path}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting certificate");
            StatusMessage = $"❌ Error: {ex.Message}";
        }
    }

    private void RefreshInstalledCertificates()
    {
        try
        {
            _logger.LogDebug("Refreshing installed certificates list");

            var certs = _certificateStore.GetCertificates(
                StoreLocation.CurrentUser,
                StoreName.My);

            InstalledCertificates.Clear();

            foreach (var cert in certs)
            {
                // Only show certificates with private keys (signing certificates)
                if (cert.HasPrivateKey)
                {
                    InstalledCertificates.Add(new CertificateViewModel(cert));
                }
            }

            _logger.LogInformation("Found {Count} installed signing certificates", InstalledCertificates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing installed certificates");
            StatusMessage = $"⚠️ Could not load installed certificates: {ex.Message}";
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }

    private async Task Check1PasswordAvailabilityAsync()
    {
        try
        {
            _logger.LogDebug("Checking 1Password availability");
            StatusMessage = "Checking 1Password CLI...";

            Is1PasswordAvailable = await _onePasswordService.IsAvailableAsync();

            if (Is1PasswordAvailable)
            {
                StatusMessage = "✅ 1Password CLI is available and authenticated";
                _logger.LogInformation("1Password integration is available");
            }
            else
            {
                StatusMessage = "ℹ️ 1Password CLI not available. Install from: https://1password.com/downloads/command-line/";
                _logger.LogInformation("1Password integration not available");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking 1Password availability");
            Is1PasswordAvailable = false;
            StatusMessage = $"⚠️ Error checking 1Password: {ex.Message}";
        }
    }

    private async Task SaveTo1PasswordAsync(X509Certificate2? cert)
    {
        if (cert == null) return;

        IsGenerating = true;
        StatusMessage = "Saving to 1Password...";

        try
        {
            _logger.LogInformation("Saving certificate to 1Password");

            // Generate a password for the PFX
            var password = $"PR{Guid.NewGuid():N}!";

            var title = $"PromptResponse - {CommonName}";

            var success = await _onePasswordService.StoreCertificateAsync(cert, password, title);

            if (success)
            {
                StatusMessage = $"✅ Certificate saved to 1Password as '{title}'\n🔑 Password stored securely in associated note";
                _logger.LogInformation("Certificate saved to 1Password successfully");
            }
            else
            {
                StatusMessage = "❌ Failed to save to 1Password (check logs)";
                _logger.LogWarning("Failed to save certificate to 1Password");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving to 1Password");
            StatusMessage = $"❌ Error: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    #endregion
}

/// <summary>
/// ViewModel wrapper for X509Certificate2
/// </summary>
public class CertificateViewModel
{
    public CertificateViewModel(X509Certificate2 certificate)
    {
        Certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));

        Subject = certificate.Subject;
        Issuer = certificate.Issuer;
        Thumbprint = certificate.Thumbprint;
        NotBefore = certificate.NotBefore;
        NotAfter = certificate.NotAfter;
        HasPrivateKey = certificate.HasPrivateKey;
        IsValid = certificate.NotBefore <= DateTime.Now && certificate.NotAfter >= DateTime.Now;

        // Extract email from subject
        var emailMatch = System.Text.RegularExpressions.Regex.Match(Subject, @"E=([^,]+)");
        Email = emailMatch.Success ? emailMatch.Groups[1].Value : string.Empty;

        // Determine usage from extensions
        var ekuExtension = certificate.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .FirstOrDefault();

        if (ekuExtension != null)
        {
            var oids = ekuExtension.EnhancedKeyUsages.Cast<Oid>().Select(o => o.Value).ToList();
            CanSignEmails = oids.Contains("1.3.6.1.5.5.7.3.4");
            CanSignDocuments = oids.Contains("1.3.6.1.4.1.311.10.3.12");
            CanSignCode = oids.Contains("1.3.6.1.5.5.7.3.3");
        }
    }

    public X509Certificate2 Certificate { get; }
    public string Subject { get; }
    public string Issuer { get; }
    public string Thumbprint { get; }
    public string Email { get; }
    public DateTime NotBefore { get; }
    public DateTime NotAfter { get; }
    public bool HasPrivateKey { get; }
    public bool IsValid { get; }
    public bool CanSignEmails { get; }
    public bool CanSignDocuments { get; }
    public bool CanSignCode { get; }

    public string UsageDescription
    {
        get
        {
            var usages = new List<string>();
            if (CanSignEmails) usages.Add("Email");
            if (CanSignDocuments) usages.Add("Documents");
            if (CanSignCode) usages.Add("Code");
            return usages.Any() ? string.Join(", ", usages) : "Unknown";
        }
    }

    public string ValidityDescription => IsValid
        ? $"Valid until {NotAfter:yyyy-MM-dd}"
        : NotAfter < DateTime.Now
            ? "⚠️ Expired"
            : "⏳ Not yet valid";
}
