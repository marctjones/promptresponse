using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Desktop.Services;
using Xunit;

namespace PromptResponse.Desktop.Tests.Services;

/// <summary>
/// Unit tests for S3SubmissionService.
/// Tests form submission to S3 using pre-signed POST policies.
/// </summary>
public class S3SubmissionServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private readonly IAprSerializer _serializer;
    private readonly Mock<ILogger<S3SubmissionService>> _mockLogger;
    private readonly S3SubmissionService _service;

    public S3SubmissionServiceTests()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _serializer = new AprJsonSerializer();
        _mockLogger = new Mock<ILogger<S3SubmissionService>>();
        _service = new S3SubmissionService(_serializer, _mockLogger.Object, _httpClient);
    }

    #region SubmitFormAsync Tests

    [Fact]
    public async Task SubmitFormAsync_WithNullDocument_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.SubmitFormAsync(null!));
    }

    [Fact]
    public async Task SubmitFormAsync_WithNoSubmissionConfig_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateFilledForm(submissionConfig: null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SubmitFormAsync(document));
        ex.Message.Should().Contain("does not have submission configuration");
    }

    [Fact]
    public async Task SubmitFormAsync_WithExpiredConfig_ThrowsInvalidOperationException()
    {
        // Arrange
        var document = CreateFilledForm(CreateExpiredSubmissionConfig());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SubmitFormAsync(document));
        ex.Message.Should().Contain("expired");
    }

    [Fact]
    public async Task SubmitFormAsync_WithUnsupportedType_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = CreateValidSubmissionConfig();
        config.Type = "unsupported-type";
        var document = CreateFilledForm(config);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SubmitFormAsync(document));
        ex.Message.Should().Contain("Unsupported submission type");
    }

    [Fact]
    public async Task SubmitFormAsync_WithValidConfig_SubmitsToCorrectUrl()
    {
        // Arrange
        var config = CreateValidSubmissionConfig();
        config.Url = "https://test-bucket.s3.us-east-1.amazonaws.com/";
        var document = CreateFilledForm(config);
        SetupHttpResponse(HttpStatusCode.NoContent, "");

        string? capturedUrl = null;
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedUrl = req.RequestUri?.ToString())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.NoContent });

        // Act
        await _service.SubmitFormAsync(document);

        // Assert
        capturedUrl.Should().Be("https://test-bucket.s3.us-east-1.amazonaws.com/");
    }

    [Fact]
    public async Task SubmitFormAsync_GeneratesDefaultFilename_WhenNotProvided()
    {
        // Arrange
        var document = CreateFilledForm(CreateValidSubmissionConfig());
        document.Metadata.TemplateId = "my-form";
        SetupHttpResponse(HttpStatusCode.NoContent, "");

        // Act
        var result = await _service.SubmitFormAsync(document);

        // Assert - result is the full key path including prefix
        result.Should().Contain("my-form-");
        result.Should().EndWith(".aprf");
    }

    [Fact]
    public async Task SubmitFormAsync_UsesProvidedFilename()
    {
        // Arrange
        var document = CreateFilledForm(CreateValidSubmissionConfig());
        SetupHttpResponse(HttpStatusCode.NoContent, "");

        // Act
        var result = await _service.SubmitFormAsync(document, "custom-name.aprf");

        // Assert
        result.Should().Contain("custom-name.aprf");
    }

    [Fact]
    public async Task SubmitFormAsync_ReplacesFilenameInKeyField()
    {
        // Arrange
        var config = CreateValidSubmissionConfig();
        config.Fields = new Dictionary<string, string>
        {
            ["key"] = "submissions/${filename}",
            ["acl"] = "private"
        };
        var document = CreateFilledForm(config);
        SetupHttpResponse(HttpStatusCode.NoContent, "");

        // Act - test via return value since multipart content is hard to capture after async
        var result = await _service.SubmitFormAsync(document, "test-file.aprf");

        // Assert - verify the key was correctly generated
        result.Should().Be("submissions/test-file.aprf");
    }

    [Fact]
    public async Task SubmitFormAsync_WhenHttpErrorReturned_ThrowsHttpRequestException()
    {
        // Arrange
        var document = CreateFilledForm(CreateValidSubmissionConfig());
        SetupHttpResponse(HttpStatusCode.Forbidden, "Access Denied");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => _service.SubmitFormAsync(document));
        ex.Message.Should().Contain("Forbidden");
    }

    [Fact]
    public async Task SubmitFormAsync_OnSuccess_ReturnsKey()
    {
        // Arrange
        var config = CreateValidSubmissionConfig();
        config.Fields!["key"] = "uploads/${filename}";
        var document = CreateFilledForm(config);
        SetupHttpResponse(HttpStatusCode.NoContent, "");

        // Act
        var result = await _service.SubmitFormAsync(document, "myform.aprf");

        // Assert
        result.Should().Be("uploads/myform.aprf");
    }

    #endregion

    #region CanSubmit Tests

    [Fact]
    public void CanSubmit_WithNullDocument_ReturnsFalse()
    {
        // Act
        var result = _service.CanSubmit(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanSubmit_WithNoSubmissionConfig_ReturnsFalse()
    {
        // Arrange
        var document = CreateFilledForm(submissionConfig: null);

        // Act
        var result = _service.CanSubmit(document);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanSubmit_WithExpiredConfig_ReturnsFalse()
    {
        // Arrange
        var document = CreateFilledForm(CreateExpiredSubmissionConfig());

        // Act
        var result = _service.CanSubmit(document);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanSubmit_WithValidConfig_ReturnsTrue()
    {
        // Arrange
        var document = CreateFilledForm(CreateValidSubmissionConfig());

        // Act
        var result = _service.CanSubmit(document);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region GetExpirationStatus Tests

    [Fact]
    public void GetExpirationStatus_WithNullDocument_ReturnsNotExpiredNoTimeRemaining()
    {
        // Act
        var (isExpired, timeRemaining) = _service.GetExpirationStatus(null!);

        // Assert
        isExpired.Should().BeFalse();
        timeRemaining.Should().BeNull();
    }

    [Fact]
    public void GetExpirationStatus_WithNoSubmissionConfig_ReturnsNotExpiredNoTimeRemaining()
    {
        // Arrange
        var document = CreateFilledForm(submissionConfig: null);

        // Act
        var (isExpired, timeRemaining) = _service.GetExpirationStatus(document);

        // Assert
        isExpired.Should().BeFalse();
        timeRemaining.Should().BeNull();
    }

    [Fact]
    public void GetExpirationStatus_WithExpiredConfig_ReturnsExpired()
    {
        // Arrange
        var document = CreateFilledForm(CreateExpiredSubmissionConfig());

        // Act
        var (isExpired, timeRemaining) = _service.GetExpirationStatus(document);

        // Assert
        isExpired.Should().BeTrue();
    }

    [Fact]
    public void GetExpirationStatus_WithValidConfig_ReturnsNotExpiredWithTimeRemaining()
    {
        // Arrange
        var config = CreateValidSubmissionConfig();
        config.ExpiresAt = DateTime.UtcNow.AddDays(7);
        var document = CreateFilledForm(config);

        // Act
        var (isExpired, timeRemaining) = _service.GetExpirationStatus(document);

        // Assert
        isExpired.Should().BeFalse();
        timeRemaining.Should().NotBeNull();
        timeRemaining!.Value.TotalDays.Should().BeGreaterThan(6);
    }

    #endregion

    #region Helper Methods

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
    }

    private static AprDocument CreateFilledForm(SubmissionConfig? submissionConfig)
    {
        return new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata
            {
                Title = "Test Form",
                TemplateId = "test-template",
                SubmissionConfig = submissionConfig
            },
            Sections = new List<Section>
            {
                new Section
                {
                    Id = "section_1",
                    Title = "Section 1",
                    Prompts = new List<Prompt>
                    {
                        new Prompt { Id = "prompt_1", Label = "Test", Response = "Value" }
                    }
                }
            }
        };
    }

    private static SubmissionConfig CreateValidSubmissionConfig()
    {
        return new SubmissionConfig
        {
            Type = "s3-presigned-post",
            Url = "https://test-bucket.s3.us-east-1.amazonaws.com/",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Fields = new Dictionary<string, string>
            {
                ["key"] = "submissions/${filename}",
                ["acl"] = "private",
                ["Content-Type"] = "application/json",
                ["Policy"] = "base64policy",
                ["X-Amz-Algorithm"] = "AWS4-HMAC-SHA256",
                ["X-Amz-Credential"] = "credential",
                ["X-Amz-Date"] = "20240101T000000Z",
                ["X-Amz-Signature"] = "signature"
            }
        };
    }

    private static SubmissionConfig CreateExpiredSubmissionConfig()
    {
        var config = CreateValidSubmissionConfig();
        config.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        return config;
    }

    #endregion
}
