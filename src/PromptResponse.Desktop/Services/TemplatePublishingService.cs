using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using System.Text;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Implementation of ITemplatePublishingService for publishing templates to S3.
/// </summary>
public class TemplatePublishingService : ITemplatePublishingService
{
    private readonly IAprSerializer _serializer;
    private readonly IValidator<AprDocument> _validator;
    private readonly ILogger<TemplatePublishingService> _logger;

    public TemplatePublishingService(
        IAprSerializer serializer,
        IValidator<AprDocument> validator,
        ILogger<TemplatePublishingService> logger)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<string> PublishTemplateAsync(
        S3BucketConfig config,
        AprDocument template,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        // Validate template
        var (isValid, errorMessage) = ValidateForPublishing(template);
        if (!isValid)
        {
            throw new InvalidOperationException($"Template cannot be published: {errorMessage}");
        }

        // Generate filename if not provided
        if (string.IsNullOrWhiteSpace(fileName))
        {
            var templateId = template.Metadata?.TemplateId ?? "template";
            var version = template.Metadata?.TemplateVersion ?? "1.0";
            fileName = $"{templateId}-v{version}.aprt";
        }

        // Ensure .aprt extension
        if (!fileName.EndsWith(".aprt", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".aprt";
        }

        _logger.LogInformation(
            "Publishing template '{TemplateId}' to S3 bucket '{Bucket}' as '{FileName}'",
            template.Metadata?.TemplateId,
            config.BucketName,
            fileName);

        // Serialize template
        var json = _serializer.Serialize(template);
        var contentBytes = Encoding.UTF8.GetBytes(json);

        using var client = CreateS3Client(config);

        try
        {
            var request = new PutObjectRequest
            {
                BucketName = config.BucketName,
                Key = fileName,
                ContentBody = json,
                ContentType = "application/json",
                Metadata =
                {
                    ["template-id"] = template.Metadata?.TemplateId ?? "unknown",
                    ["template-version"] = template.Metadata?.TemplateVersion ?? "unknown",
                    ["author"] = template.Metadata?.Author ?? "unknown",
                    ["is-signed"] = template.Metadata?.TemplateSignatures?.Count > 0 ? "true" : "false"
                }
            };

            var response = await client.PutObjectAsync(request, cancellationToken);

            _logger.LogInformation(
                "Successfully published template to S3: {Key} (ETag: {ETag})",
                fileName,
                response.ETag);

            return fileName;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to publish template to S3");
            throw new InvalidOperationException($"Failed to publish template: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public (bool IsValid, string? ErrorMessage) ValidateForPublishing(AprDocument document)
    {
        if (document == null)
        {
            return (false, "Document is null");
        }

        // Must be a template
        if (document.DocumentType != DocumentType.Template)
        {
            return (false, "Document must be a template (not a filled form)");
        }

        // Must have metadata
        if (document.Metadata == null)
        {
            return (false, "Document must have metadata");
        }

        // Must have template ID
        if (string.IsNullOrWhiteSpace(document.Metadata.TemplateId))
        {
            return (false, "Template must have a templateId");
        }

        // Must be signed
        if (document.Metadata.TemplateSignatures == null ||
            document.Metadata.TemplateSignatures.Count == 0)
        {
            return (false, "Template must be digitally signed before publishing");
        }

        // Must pass validation
        var validationResult = _validator.Validate(document);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.Message));
            return (false, $"Template validation failed: {errors}");
        }

        // All checks passed
        return (true, null);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<S3Object>> ListPublishedTemplatesAsync(
        S3BucketConfig config,
        string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        _logger.LogInformation(
            "Listing published templates in bucket '{Bucket}' with prefix '{Prefix}'",
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

            // Filter to only .aprt files
            var templates = response.S3Objects
                .Where(obj => obj.Key.EndsWith(".aprt", StringComparison.OrdinalIgnoreCase))
                .Select(obj => new S3Object(
                    obj.Key,
                    obj.Size,
                    obj.LastModified,
                    obj.ETag))
                .ToList();

            _logger.LogInformation("Found {Count} published templates", templates.Count);

            return templates;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to list templates from S3");
            throw new InvalidOperationException($"Failed to list templates: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task UnpublishTemplateAsync(
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

        _logger.LogInformation("Unpublishing template '{Key}' from bucket '{Bucket}'", key, config.BucketName);

        using var client = CreateS3Client(config);

        try
        {
            var request = new DeleteObjectRequest
            {
                BucketName = config.BucketName,
                Key = key
            };

            await client.DeleteObjectAsync(request, cancellationToken);

            _logger.LogInformation("Successfully unpublished template: {Key}", key);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to unpublish template");
            throw new InvalidOperationException($"Failed to unpublish template: {ex.Message}", ex);
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
