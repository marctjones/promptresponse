using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Implementation of IS3BrowserService using AWS SDK for .NET.
/// </summary>
public class S3BrowserService : IS3BrowserService
{
    private readonly IAprSerializer _serializer;
    private readonly ILogger<S3BrowserService> _logger;

    public S3BrowserService(
        IAprSerializer serializer,
        ILogger<S3BrowserService> logger)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<S3Object>> ListObjectsAsync(
        S3BucketConfig config,
        string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        _logger.LogInformation(
            "Listing objects in bucket {Bucket} with prefix '{Prefix}'",
            config.BucketName,
            prefix ?? "(none)");

        using var client = CreateS3Client(config);

        try
        {
            var request = new ListObjectsV2Request
            {
                BucketName = config.BucketName,
                Prefix = prefix
            };

            var response = await client.ListObjectsV2Async(request, cancellationToken);

            var objects = response.S3Objects
                .Select(obj => new S3Object(
                    obj.Key,
                    obj.Size,
                    obj.LastModified,
                    obj.ETag))
                .ToList();

            _logger.LogInformation("Found {Count} objects in bucket", objects.Count);

            return objects;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to list objects in bucket {Bucket}", config.BucketName);
            throw new InvalidOperationException(
                $"Failed to list objects: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<AprDocument> DownloadDocumentAsync(
        S3BucketConfig config,
        string key,
        CancellationToken cancellationToken = default)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Object key cannot be empty", nameof(key));
        }

        _logger.LogInformation("Downloading object {Key} from bucket {Bucket}", key, config.BucketName);

        using var client = CreateS3Client(config);

        try
        {
            var request = new GetObjectRequest
            {
                BucketName = config.BucketName,
                Key = key
            };

            using var response = await client.GetObjectAsync(request, cancellationToken);
            using var stream = response.ResponseStream;

            var document = await _serializer.DeserializeAsync(stream);

            _logger.LogInformation("Successfully downloaded and deserialized document");

            return document;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogError("Object {Key} not found in bucket {Bucket}", key, config.BucketName);
            throw new FileNotFoundException($"Object '{key}' not found in bucket '{config.BucketName}'", key);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to download object {Key}", key);
            throw new InvalidOperationException($"Failed to download object: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TestConnectionAsync(
        S3BucketConfig config,
        CancellationToken cancellationToken = default)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        _logger.LogInformation("Testing connection to bucket {Bucket}", config.BucketName);

        using var client = CreateS3Client(config);

        try
        {
            // Try to check if bucket exists and we have access
            var request = new ListObjectsV2Request
            {
                BucketName = config.BucketName,
                MaxKeys = 1
            };

            await client.ListObjectsV2Async(request, cancellationToken);

            _logger.LogInformation("Connection test successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Connection test failed");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteObjectAsync(
        S3BucketConfig config,
        string key,
        CancellationToken cancellationToken = default)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Object key cannot be empty", nameof(key));
        }

        _logger.LogInformation("Deleting object {Key} from bucket {Bucket}", key, config.BucketName);

        using var client = CreateS3Client(config);

        try
        {
            var request = new DeleteObjectRequest
            {
                BucketName = config.BucketName,
                Key = key
            };

            await client.DeleteObjectAsync(request, cancellationToken);

            _logger.LogInformation("Successfully deleted object {Key}", key);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to delete object {Key}", key);
            throw new InvalidOperationException($"Failed to delete object: {ex.Message}", ex);
        }
    }

    private IAmazonS3 CreateS3Client(S3BucketConfig config)
    {
        var credentials = new BasicAWSCredentials(config.AccessKeyId, config.SecretAccessKey);

        var clientConfig = new AmazonS3Config
        {
            ServiceURL = config.ServiceUrl,
            ForcePathStyle = config.ForcePathStyle,
            AuthenticationRegion = config.Region,
            SignatureVersion = "4"
        };

        return new AmazonS3Client(credentials, clientConfig);
    }
}
