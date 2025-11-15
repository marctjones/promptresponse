using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Implementation of IS3SubmissionService for submitting forms to S3.
/// </summary>
public class S3SubmissionService : IS3SubmissionService
{
    private readonly IAprSerializer _serializer;
    private readonly ILogger<S3SubmissionService> _logger;
    private readonly HttpClient _httpClient;

    public S3SubmissionService(
        IAprSerializer serializer,
        ILogger<S3SubmissionService> logger,
        HttpClient? httpClient = null)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <inheritdoc/>
    public async Task<string> SubmitFormAsync(
        AprDocument document,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var submissionConfig = document.Metadata?.SubmissionConfig;
        if (submissionConfig == null)
        {
            throw new InvalidOperationException("Document does not have submission configuration.");
        }

        if (!submissionConfig.IsValid())
        {
            if (submissionConfig.IsExpired())
            {
                throw new InvalidOperationException(
                    $"Submission configuration expired on {submissionConfig.ExpiresAt?.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
            }
            throw new InvalidOperationException("Submission configuration is invalid.");
        }

        if (submissionConfig.Type != "s3-presigned-post")
        {
            throw new InvalidOperationException($"Unsupported submission type: {submissionConfig.Type}");
        }

        _logger.LogInformation("Submitting form to S3: {Url}", submissionConfig.Url);

        // Generate filename if not provided
        if (string.IsNullOrWhiteSpace(fileName))
        {
            var templateId = document.Metadata?.TemplateId ?? "form";
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            fileName = $"{templateId}-{timestamp}.aprf";
        }

        // Serialize document to JSON
        var json = _serializer.Serialize(document);
        var fileBytes = Encoding.UTF8.GetBytes(json);

        // Build multipart form data
        using var content = new MultipartFormDataContent();

        // Add all fields from the pre-signed POST policy
        if (submissionConfig.Fields != null)
        {
            foreach (var field in submissionConfig.Fields)
            {
                // Replace ${filename} placeholder in the key field
                var value = field.Key == "key" && field.Value.Contains("${filename}")
                    ? field.Value.Replace("${filename}", fileName)
                    : field.Value;

                content.Add(new StringContent(value), field.Key);
            }
        }

        // Add the file itself (must be last according to S3 spec)
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        content.Add(fileContent, "file", fileName);

        // Submit to S3
        try
        {
            var response = await _httpClient.PostAsync(submissionConfig.Url, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "S3 submission failed with status {StatusCode}: {ResponseBody}",
                    response.StatusCode,
                    responseBody);

                throw new HttpRequestException(
                    $"S3 submission failed with status {response.StatusCode}: {responseBody}");
            }

            // Extract the key from the response or fields
            var key = submissionConfig.Fields?.GetValueOrDefault("key")?.Replace("${filename}", fileName) ?? fileName;

            _logger.LogInformation("Successfully submitted form to S3: {Key}", key);
            return key;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to submit form to S3");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error submitting form to S3");
            throw new HttpRequestException("Unexpected error submitting form to S3", ex);
        }
    }

    /// <inheritdoc/>
    public bool CanSubmit(AprDocument document)
    {
        if (document?.Metadata?.SubmissionConfig == null)
        {
            return false;
        }

        return document.Metadata.SubmissionConfig.IsValid();
    }

    /// <inheritdoc/>
    public (bool IsExpired, TimeSpan? TimeRemaining) GetExpirationStatus(AprDocument document)
    {
        var submissionConfig = document?.Metadata?.SubmissionConfig;
        if (submissionConfig == null)
        {
            return (false, null);
        }

        var isExpired = submissionConfig.IsExpired();
        var timeRemaining = submissionConfig.TimeUntilExpiration();

        return (isExpired, timeRemaining);
    }
}
