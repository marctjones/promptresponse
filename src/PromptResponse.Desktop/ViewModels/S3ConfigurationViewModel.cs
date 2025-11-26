using System.Windows.Input;
using PromptResponse.Core.Models;
using PromptResponse.Core.Services;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// ViewModel for the S3 Configuration dialog.
/// Allows template authors to configure S3 submission settings.
/// </summary>
public class S3ConfigurationViewModel : ViewModelBase
{
    private readonly S3PolicyGenerator _policyGenerator;
    private readonly AprDocument _document;
    private readonly Action<bool> _closeCallback;

    private string _bucketName = string.Empty;
    private string _region = "us-east-1";
    private string _keyPrefix = "submissions/";
    private string _accessKeyId = string.Empty;
    private string _secretAccessKey = string.Empty;
    private string _customEndpoint = string.Empty;
    private bool _usePathStyle;
    private int _expirationDays = 30;
    private long _maxFileSizeMB = 10;
    private string _statusMessage = string.Empty;
    private bool _isConfigured;
    private DateTime? _expiresAt;
    private bool _isBusy;
    private string _templateSourceUrl = string.Empty;

    public S3ConfigurationViewModel(
        S3PolicyGenerator policyGenerator,
        AprDocument document,
        Action<bool> closeCallback)
    {
        _policyGenerator = policyGenerator;
        _document = document;
        _closeCallback = closeCallback;

        // Load existing config if present
        LoadExistingConfig();

        GenerateConfigCommand = new RelayCommand(GenerateConfig, CanGenerateConfig);
        RemoveConfigCommand = new RelayCommand(RemoveConfig, () => IsConfigured);
        RefreshExpirationCommand = new RelayCommand(RefreshExpiration, () => IsConfigured && CanGenerateConfig());
        CancelCommand = new RelayCommand(() => _closeCallback(false));
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, CanGenerateConfig);
    }

    #region Properties

    public string BucketName
    {
        get => _bucketName;
        set
        {
            if (SetProperty(ref _bucketName, value))
                UpdateCommandStates();
        }
    }

    public string Region
    {
        get => _region;
        set => SetProperty(ref _region, value);
    }

    public string KeyPrefix
    {
        get => _keyPrefix;
        set => SetProperty(ref _keyPrefix, value);
    }

    public string AccessKeyId
    {
        get => _accessKeyId;
        set
        {
            if (SetProperty(ref _accessKeyId, value))
                UpdateCommandStates();
        }
    }

    public string SecretAccessKey
    {
        get => _secretAccessKey;
        set
        {
            if (SetProperty(ref _secretAccessKey, value))
                UpdateCommandStates();
        }
    }

    public string CustomEndpoint
    {
        get => _customEndpoint;
        set => SetProperty(ref _customEndpoint, value);
    }

    public bool UsePathStyle
    {
        get => _usePathStyle;
        set => SetProperty(ref _usePathStyle, value);
    }

    public int ExpirationDays
    {
        get => _expirationDays;
        set => SetProperty(ref _expirationDays, Math.Max(1, Math.Min(365, value)));
    }

    public long MaxFileSizeMB
    {
        get => _maxFileSizeMB;
        set => SetProperty(ref _maxFileSizeMB, Math.Max(1, Math.Min(100, value)));
    }

    /// <summary>
    /// Gets or sets the URL where this template will be hosted.
    /// Used by form fillers to check for template updates.
    /// </summary>
    public string TemplateSourceUrl
    {
        get => _templateSourceUrl;
        set => SetProperty(ref _templateSourceUrl, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsConfigured
    {
        get => _isConfigured;
        private set
        {
            if (SetProperty(ref _isConfigured, value))
            {
                OnPropertyChanged(nameof(ConfigStatusText));
                OnPropertyChanged(nameof(ExpirationStatusText));
                UpdateCommandStates();
            }
        }
    }

    public DateTime? ExpiresAt
    {
        get => _expiresAt;
        private set
        {
            if (SetProperty(ref _expiresAt, value))
            {
                OnPropertyChanged(nameof(ExpirationStatusText));
                OnPropertyChanged(nameof(IsExpired));
                OnPropertyChanged(nameof(IsExpiringSoon));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string ConfigStatusText => IsConfigured
        ? "S3 submission is configured"
        : "S3 submission is not configured";

    public string ExpirationStatusText
    {
        get
        {
            if (!IsConfigured || ExpiresAt == null)
                return string.Empty;

            var remaining = ExpiresAt.Value - DateTime.UtcNow;
            if (remaining.TotalDays < 0)
                return "EXPIRED";
            if (remaining.TotalDays < 1)
                return $"Expires in {remaining.Hours} hours";
            if (remaining.TotalDays < 7)
                return $"Expires in {(int)remaining.TotalDays} days";
            return $"Expires on {ExpiresAt.Value:yyyy-MM-dd}";
        }
    }

    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;

    public bool IsExpiringSoon => ExpiresAt.HasValue &&
        !IsExpired &&
        (ExpiresAt.Value - DateTime.UtcNow).TotalDays < 7;

    public bool HasCustomEndpoint => !string.IsNullOrWhiteSpace(CustomEndpoint);

    #endregion

    #region Commands

    public ICommand GenerateConfigCommand { get; }
    public ICommand RemoveConfigCommand { get; }
    public ICommand RefreshExpirationCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand TestConnectionCommand { get; }

    #endregion

    #region Methods

    private void LoadExistingConfig()
    {
        var config = _document.Metadata.SubmissionConfig;
        if (config == null || config.Type != "s3-presigned-post")
        {
            IsConfigured = false;
            return;
        }

        IsConfigured = true;
        ExpiresAt = config.ExpiresAt;

        // Try to parse existing URL for bucket and region
        if (!string.IsNullOrEmpty(config.Url))
        {
            ParseExistingUrl(config.Url);
        }

        // Extract key prefix from the key field
        if (config.Fields?.TryGetValue("key", out var keyField) == true && keyField != null)
        {
            var prefix = keyField.Replace("${filename}", "");
            if (!string.IsNullOrEmpty(prefix))
                KeyPrefix = prefix;
        }

        // Load template source URL if set
        if (!string.IsNullOrEmpty(_document.Metadata.TemplateSourceUrl))
        {
            TemplateSourceUrl = _document.Metadata.TemplateSourceUrl;
        }

        StatusMessage = "Existing S3 configuration loaded. Enter credentials to refresh.";
    }

    private void ParseExistingUrl(string url)
    {
        try
        {
            var uri = new Uri(url);

            // Check for standard AWS S3 URL format
            // https://bucket.s3.region.amazonaws.com/
            if (uri.Host.Contains(".s3.") && uri.Host.EndsWith(".amazonaws.com"))
            {
                var parts = uri.Host.Split('.');
                if (parts.Length >= 4)
                {
                    BucketName = parts[0];
                    Region = parts[2];
                }
            }
            // Check for path-style URL (MinIO, etc.)
            else if (uri.PathAndQuery.Length > 1)
            {
                CustomEndpoint = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
                var pathParts = uri.AbsolutePath.Trim('/').Split('/');
                if (pathParts.Length > 0)
                    BucketName = pathParts[0];
                UsePathStyle = true;
            }
            else
            {
                CustomEndpoint = $"{uri.Scheme}://{uri.Host}";
                if (uri.Port != 80 && uri.Port != 443)
                    CustomEndpoint += $":{uri.Port}";
            }
        }
        catch
        {
            // Could not parse URL, leave fields empty
        }
    }

    private bool CanGenerateConfig()
    {
        return !string.IsNullOrWhiteSpace(BucketName) &&
               !string.IsNullOrWhiteSpace(AccessKeyId) &&
               !string.IsNullOrWhiteSpace(SecretAccessKey);
    }

    private void GenerateConfig()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Generating S3 configuration...";

            var config = new S3PolicyGenerator.S3Config
            {
                BucketName = BucketName.Trim(),
                Region = string.IsNullOrWhiteSpace(Region) ? "us-east-1" : Region.Trim(),
                KeyPrefix = KeyPrefix?.Trim() ?? "",
                AccessKeyId = AccessKeyId.Trim(),
                SecretAccessKey = SecretAccessKey.Trim(),
                Expiration = TimeSpan.FromDays(ExpirationDays),
                MaxFileSizeBytes = MaxFileSizeMB * 1024 * 1024,
                CustomEndpoint = string.IsNullOrWhiteSpace(CustomEndpoint) ? null : CustomEndpoint.Trim(),
                UsePathStyle = UsePathStyle
            };

            var submissionConfig = _policyGenerator.GenerateSubmissionConfig(config);
            _document.Metadata.SubmissionConfig = submissionConfig;
            _document.Metadata.Modified = DateTime.UtcNow;

            // Save template source URL if provided
            if (!string.IsNullOrWhiteSpace(TemplateSourceUrl))
            {
                _document.Metadata.TemplateSourceUrl = TemplateSourceUrl.Trim();
            }
            else
            {
                _document.Metadata.TemplateSourceUrl = null;
            }

            IsConfigured = true;
            ExpiresAt = submissionConfig.ExpiresAt;
            StatusMessage = $"S3 configuration generated. Expires {ExpirationStatusText}";

            // Clear sensitive data from view (but it's already in the policy)
            // User can choose to keep them for refresh operations
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RemoveConfig()
    {
        _document.Metadata.SubmissionConfig = null;
        _document.Metadata.Modified = DateTime.UtcNow;
        IsConfigured = false;
        ExpiresAt = null;
        StatusMessage = "S3 configuration removed";
    }

    private void RefreshExpiration()
    {
        GenerateConfig();
        if (IsConfigured)
        {
            StatusMessage = $"Expiration refreshed. New expiration: {ExpirationStatusText}";
        }
    }

    private async Task TestConnectionAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Testing connection...";

            // Build the endpoint URL
            string endpointUrl;
            if (!string.IsNullOrWhiteSpace(CustomEndpoint))
            {
                endpointUrl = UsePathStyle
                    ? $"{CustomEndpoint.TrimEnd('/')}/{BucketName}/"
                    : $"{CustomEndpoint.TrimEnd('/')}/";
            }
            else
            {
                endpointUrl = $"https://{BucketName}.s3.{Region}.amazonaws.com/";
            }

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, endpointUrl));

            // 403 Forbidden is expected for private buckets (credentials are valid but no ListBucket permission)
            // 200 OK means public bucket or we have read permission
            // 404 means bucket doesn't exist
            if (response.StatusCode == System.Net.HttpStatusCode.OK ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                StatusMessage = "Connection successful! Bucket is accessible.";
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                StatusMessage = "Warning: Bucket not found. Check bucket name.";
            }
            else
            {
                StatusMessage = $"Warning: Received HTTP {(int)response.StatusCode}";
            }
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            StatusMessage = "Connection timed out";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateCommandStates()
    {
        (GenerateConfigCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RemoveConfigCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RefreshExpirationCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (TestConnectionCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    #endregion
}

/// <summary>
/// Async relay command implementation.
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute?.Invoke() ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;

        _isExecuting = true;
        RaiseCanExecuteChanged();

        try
        {
            await _execute();
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
