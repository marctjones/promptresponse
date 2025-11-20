using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Services;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Implementation of ITemplateGalleryService for browsing and downloading templates.
/// </summary>
public class TemplateGalleryService : ITemplateGalleryService
{
    private readonly IAprSerializer _serializer;
    private readonly ISignatureVerificationService? _signatureService;
    private readonly ILogger<TemplateGalleryService> _logger;

    public TemplateGalleryService(
        IAprSerializer serializer,
        ILogger<TemplateGalleryService> logger,
        ISignatureVerificationService? signatureService = null)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _signatureService = signatureService;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TemplateGalleryItem>> BrowseTemplatesAsync(
        S3BucketConfig config,
        string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        _logger.LogInformation(
            "Browsing templates in bucket '{Bucket}' with prefix '{Prefix}'",
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

            // Filter to only .aprt files and get metadata
            var templates = new List<TemplateGalleryItem>();

            foreach (var obj in response.S3Objects.Where(o =>
                o.Key.EndsWith(".aprt", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    // Get object metadata
                    var metadataRequest = new GetObjectMetadataRequest
                    {
                        BucketName = config.BucketName,
                        Key = obj.Key
                    };

                    var metadataResponse = await client.GetObjectMetadataAsync(metadataRequest, cancellationToken);

                    // Extract metadata
                    var templateId = metadataResponse.Metadata["x-amz-meta-template-id"] ?? Path.GetFileNameWithoutExtension(obj.Key);
                    var version = metadataResponse.Metadata["x-amz-meta-template-version"] ?? "unknown";
                    var author = metadataResponse.Metadata["x-amz-meta-author"] ?? "unknown";
                    var isSigned = metadataResponse.Metadata["x-amz-meta-is-signed"] == "true";

                    // For title and description, we'd need to download the full file
                    // For now, use template ID as title
                    var item = new TemplateGalleryItem(
                        obj.Key,
                        templateId,
                        null, // Title - could download and parse if needed
                        null, // Description - could download and parse if needed
                        author,
                        version,
                        isSigned,
                        obj.Size,
                        obj.LastModified);

                    templates.Add(item);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get metadata for template {Key}", obj.Key);
                }
            }

            _logger.LogInformation("Found {Count} templates in gallery", templates.Count);

            return templates;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to browse templates");
            throw new InvalidOperationException($"Failed to browse templates: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<AprDocument> DownloadTemplateAsync(
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
            throw new ArgumentException("Key cannot be empty", nameof(key));
        }

        _logger.LogInformation("Downloading template '{Key}' from bucket '{Bucket}'", key, config.BucketName);

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

            // Verify it's actually a template
            if (document.DocumentType != DocumentType.Template)
            {
                _logger.LogWarning("Downloaded file is not a template: {Key}", key);
                throw new InvalidOperationException($"File '{key}' is not a template");
            }

            _logger.LogInformation("Successfully downloaded template: {TemplateId}", document.Metadata?.TemplateId);

            return document;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogError("Template {Key} not found", key);
            throw new FileNotFoundException($"Template '{key}' not found in gallery", key);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to download template");
            throw new InvalidOperationException($"Failed to download template: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<(AprDocument Template, SignatureVerificationResult? VerificationResult)> DownloadAndVerifyTemplateAsync(
        S3BucketConfig config,
        string key,
        CancellationToken cancellationToken = default)
    {
        var template = await DownloadTemplateAsync(config, key, cancellationToken);

        SignatureVerificationResult? verificationResult = null;

        // Verify signatures if service is available and template has signatures
        if (_signatureService != null &&
            template.Metadata?.TemplateSignatures != null &&
            template.Metadata.TemplateSignatures.Count > 0)
        {
            _logger.LogInformation("Verifying template signatures for {TemplateId}", template.Metadata.TemplateId);

            try
            {
                verificationResult = await _signatureService.VerifyTemplateSignatureAsync(template);

                if (verificationResult.IsValid)
                {
                    _logger.LogInformation("Template signature verified successfully");
                }
                else
                {
                    _logger.LogWarning("Template signature verification failed: {Reason}", verificationResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying template signature");
                verificationResult = new SignatureVerificationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Signature verification error: {ex.Message}"
                };
            }
        }
        else if (template.Metadata?.TemplateSignatures == null || template.Metadata.TemplateSignatures.Count == 0)
        {
            _logger.LogWarning("Template has no signatures");
            verificationResult = new SignatureVerificationResult
            {
                IsValid = false,
                ErrorMessage = "Template is not signed"
            };
        }

        return (template, verificationResult);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TemplateGalleryItem>> SearchTemplatesAsync(
        S3BucketConfig config,
        string searchTerm,
        string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await BrowseTemplatesAsync(config, prefix, cancellationToken);
        }

        var allTemplates = await BrowseTemplatesAsync(config, prefix, cancellationToken);

        var searchLower = searchTerm.ToLowerInvariant();

        var matches = allTemplates.Where(t =>
            t.TemplateId.ToLowerInvariant().Contains(searchLower) ||
            (t.Title?.ToLowerInvariant().Contains(searchLower) ?? false) ||
            (t.Description?.ToLowerInvariant().Contains(searchLower) ?? false) ||
            (t.Author?.ToLowerInvariant().Contains(searchLower) ?? false))
            .ToList();

        _logger.LogInformation(
            "Search for '{SearchTerm}' found {Count} matches out of {Total} templates",
            searchTerm,
            matches.Count,
            allTemplates.Count);

        return matches;
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
