using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Services;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// ViewModel for the S3 browser dialog.
/// </summary>
public class S3BrowserViewModel : ViewModelBase
{
    private readonly IS3BrowserService _s3BrowserService;
    private readonly ILogger<S3BrowserViewModel> _logger;
    private readonly Action<AprDocument>? _documentLoadedCallback;

    private string _serviceUrl = "http://localhost:9000";
    private string _bucketName = "apr-forms";
    private string _accessKeyId = string.Empty;
    private string _secretAccessKey = string.Empty;
    private string _region = "us-east-1";
    private bool _forcePathStyle = true;
    private string _prefix = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isConnected;
    private bool _isLoading;
    private S3Object? _selectedObject;

    public S3BrowserViewModel(
        IS3BrowserService s3BrowserService,
        ILogger<S3BrowserViewModel> logger,
        Action<AprDocument>? documentLoadedCallback = null)
    {
        _s3BrowserService = s3BrowserService ?? throw new ArgumentNullException(nameof(s3BrowserService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _documentLoadedCallback = documentLoadedCallback;

        Objects = new ObservableCollection<S3Object>();

        // Commands
        TestConnectionCommand = new RelayCommand(async () => await TestConnectionAsync());
        RefreshCommand = new RelayCommand(async () => await ListObjectsAsync(), () => _isConnected);
        DownloadCommand = new RelayCommand(async () => await DownloadSelectedAsync(), () => _selectedObject != null);
        DeleteCommand = new RelayCommand(async () => await DeleteSelectedAsync(), () => _selectedObject != null);

        _logger.LogInformation("S3BrowserViewModel initialized");
    }

    #region Properties

    /// <summary>
    /// Gets or sets the S3 service URL.
    /// </summary>
    public string ServiceUrl
    {
        get => _serviceUrl;
        set => SetProperty(ref _serviceUrl, value);
    }

    /// <summary>
    /// Gets or sets the bucket name.
    /// </summary>
    public string BucketName
    {
        get => _bucketName;
        set => SetProperty(ref _bucketName, value);
    }

    /// <summary>
    /// Gets or sets the access key ID.
    /// </summary>
    public string AccessKeyId
    {
        get => _accessKeyId;
        set => SetProperty(ref _accessKeyId, value);
    }

    /// <summary>
    /// Gets or sets the secret access key.
    /// </summary>
    public string SecretAccessKey
    {
        get => _secretAccessKey;
        set => SetProperty(ref _secretAccessKey, value);
    }

    /// <summary>
    /// Gets or sets the AWS region.
    /// </summary>
    public string Region
    {
        get => _region;
        set => SetProperty(ref _region, value);
    }

    /// <summary>
    /// Gets or sets whether to use path-style access.
    /// </summary>
    public bool ForcePathStyle
    {
        get => _forcePathStyle;
        set => SetProperty(ref _forcePathStyle, value);
    }

    /// <summary>
    /// Gets or sets the prefix filter for listing objects.
    /// </summary>
    public string Prefix
    {
        get => _prefix;
        set => SetProperty(ref _prefix, value);
    }

    /// <summary>
    /// Gets or sets the status message.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Gets whether connected to S3.
    /// </summary>
    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (SetProperty(ref _isConnected, value))
            {
                ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets whether a loading operation is in progress.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    /// Gets or sets the selected S3 object.
    /// </summary>
    public S3Object? SelectedObject
    {
        get => _selectedObject;
        set
        {
            if (SetProperty(ref _selectedObject, value))
            {
                ((RelayCommand)DownloadCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DeleteCommand).RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Gets the collection of S3 objects.
    /// </summary>
    public ObservableCollection<S3Object> Objects { get; }

    #endregion

    #region Commands

    public ICommand TestConnectionCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand DownloadCommand { get; }
    public ICommand DeleteCommand { get; }

    #endregion

    #region Methods

    private S3BucketConfig CreateConfig()
    {
        return new S3BucketConfig
        {
            ServiceUrl = ServiceUrl,
            BucketName = BucketName,
            AccessKeyId = AccessKeyId,
            SecretAccessKey = SecretAccessKey,
            Region = Region,
            ForcePathStyle = ForcePathStyle
        };
    }

    private async Task TestConnectionAsync()
    {
        _logger.LogInformation("Testing S3 connection to {Url}/{Bucket}", ServiceUrl, BucketName);

        try
        {
            IsLoading = true;
            StatusMessage = "Testing connection...";

            var config = CreateConfig();
            var success = await _s3BrowserService.TestConnectionAsync(config);

            if (success)
            {
                IsConnected = true;
                StatusMessage = $"Connected to {BucketName}";
                _logger.LogInformation("S3 connection successful");

                // Automatically list objects after connecting
                await ListObjectsAsync();
            }
            else
            {
                IsConnected = false;
                StatusMessage = "Connection failed. Check your credentials and bucket name.";
                _logger.LogWarning("S3 connection failed");
            }
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusMessage = $"Connection error: {ex.Message}";
            _logger.LogError(ex, "Error testing S3 connection");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ListObjectsAsync()
    {
        _logger.LogInformation("Listing S3 objects with prefix '{Prefix}'", Prefix);

        try
        {
            IsLoading = true;
            StatusMessage = "Loading objects...";

            var config = CreateConfig();
            var objects = await _s3BrowserService.ListObjectsAsync(
                config,
                string.IsNullOrWhiteSpace(Prefix) ? null : Prefix);

            Objects.Clear();
            foreach (var obj in objects.OrderByDescending(o => o.LastModified))
            {
                Objects.Add(obj);
            }

            StatusMessage = $"Found {Objects.Count} object(s)";
            _logger.LogInformation("Listed {Count} objects", Objects.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error listing objects: {ex.Message}";
            _logger.LogError(ex, "Error listing S3 objects");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DownloadSelectedAsync()
    {
        if (SelectedObject == null)
        {
            return;
        }

        _logger.LogInformation("Downloading S3 object: {Key}", SelectedObject.Key);

        try
        {
            IsLoading = true;
            StatusMessage = $"Downloading {SelectedObject.Key}...";

            var config = CreateConfig();
            var document = await _s3BrowserService.DownloadDocumentAsync(config, SelectedObject.Key);

            StatusMessage = $"Downloaded: {SelectedObject.Key}";
            _logger.LogInformation("Successfully downloaded {Key}", SelectedObject.Key);

            // Invoke callback to load document into main view
            _documentLoadedCallback?.Invoke(document);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error downloading: {ex.Message}";
            _logger.LogError(ex, "Error downloading S3 object {Key}", SelectedObject.Key);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (SelectedObject == null)
        {
            return;
        }

        _logger.LogInformation("Deleting S3 object: {Key}", SelectedObject.Key);

        try
        {
            IsLoading = true;
            StatusMessage = $"Deleting {SelectedObject.Key}...";

            var config = CreateConfig();
            await _s3BrowserService.DeleteObjectAsync(config, SelectedObject.Key);

            var deletedKey = SelectedObject.Key;
            Objects.Remove(SelectedObject);
            SelectedObject = null;

            StatusMessage = $"Deleted: {deletedKey}";
            _logger.LogInformation("Successfully deleted {Key}", deletedKey);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting: {ex.Message}";
            _logger.LogError(ex, "Error deleting S3 object {Key}", SelectedObject?.Key);
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion
}
