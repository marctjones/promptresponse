using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Services;
using Xunit;

namespace PromptResponse.Core.Tests.Services;

/// <summary>
/// Unit tests for TemplateUpdateService.
/// Tests template fetching, version comparison, and response migration.
/// </summary>
public class TemplateUpdateServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private readonly IAprSerializer _serializer;
    private readonly Mock<ILogger<TemplateUpdateService>> _mockLogger;
    private readonly TemplateUpdateService _service;

    public TemplateUpdateServiceTests()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _serializer = new AprJsonSerializer();
        _mockLogger = new Mock<ILogger<TemplateUpdateService>>();
        _service = new TemplateUpdateService(_httpClient, _serializer, _mockLogger.Object);
    }

    #region CheckForUpdateAsync Tests

    [Fact]
    public async Task CheckForUpdateAsync_WithNoSourceUrl_ReturnsFailure()
    {
        // Arrange
        var document = CreateFilledForm(templateSourceUrl: null, templateVersion: "1.0");

        // Act
        var result = await _service.CheckForUpdateAsync(document);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No template source URL");
    }

    [Fact]
    public async Task CheckForUpdateAsync_WithEmptySourceUrl_ReturnsFailure()
    {
        // Arrange
        var document = CreateFilledForm(templateSourceUrl: "", templateVersion: "1.0");

        // Act
        var result = await _service.CheckForUpdateAsync(document);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No template source URL");
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenNewerVersionAvailable_ReturnsUpdateAvailable()
    {
        // Arrange
        var currentDoc = CreateFilledForm(
            templateSourceUrl: "https://example.com/template.aprt",
            templateVersion: "1.0");

        var newTemplate = CreateTemplate(templateVersion: "2.0");
        SetupHttpResponse(HttpStatusCode.OK, _serializer.Serialize(newTemplate));

        // Act
        var result = await _service.CheckForUpdateAsync(currentDoc);

        // Assert
        result.Success.Should().BeTrue();
        result.UpdateAvailable.Should().BeTrue();
        result.CurrentVersion.Should().Be("1.0");
        result.NewVersion.Should().Be("2.0");
        result.NewTemplate.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenSameVersion_ReturnsNoUpdate()
    {
        // Arrange
        var currentDoc = CreateFilledForm(
            templateSourceUrl: "https://example.com/template.aprt",
            templateVersion: "1.0");

        var newTemplate = CreateTemplate(templateVersion: "1.0");
        SetupHttpResponse(HttpStatusCode.OK, _serializer.Serialize(newTemplate));

        // Act
        var result = await _service.CheckForUpdateAsync(currentDoc);

        // Assert
        result.Success.Should().BeTrue();
        result.UpdateAvailable.Should().BeFalse();
        result.NewTemplate.Should().BeNull();
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenHttpError_ReturnsFailure()
    {
        // Arrange
        var document = CreateFilledForm(
            templateSourceUrl: "https://example.com/template.aprt",
            templateVersion: "1.0");

        SetupHttpResponse(HttpStatusCode.NotFound, "Not Found");

        // Act
        var result = await _service.CheckForUpdateAsync(document);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to fetch template");
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenInvalidJson_ReturnsFailure()
    {
        // Arrange
        var document = CreateFilledForm(
            templateSourceUrl: "https://example.com/template.aprt",
            templateVersion: "1.0");

        SetupHttpResponse(HttpStatusCode.OK, "{ invalid json }");

        // Act
        var result = await _service.CheckForUpdateAsync(document);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid JSON format");
    }

    [Theory]
    [InlineData("1.0", "2.0", true)]
    [InlineData("2.0", "1.0", true)]  // Different versions = update available (user decides)
    [InlineData("1.0", "1.0", false)]
    [InlineData("", "", false)]
    [InlineData(null, null, false)]
    [InlineData("v1.0", "v1.0", false)]
    [InlineData("2025-Q1", "2025-Q2", true)]
    public async Task CheckForUpdateAsync_VersionComparison_WorksCorrectly(
        string? currentVersion, string? newVersion, bool expectedUpdateAvailable)
    {
        // Arrange
        var currentDoc = CreateFilledForm(
            templateSourceUrl: "https://example.com/template.aprt",
            templateVersion: currentVersion);

        var newTemplate = CreateTemplate(templateVersion: newVersion);
        SetupHttpResponse(HttpStatusCode.OK, _serializer.Serialize(newTemplate));

        // Act
        var result = await _service.CheckForUpdateAsync(currentDoc);

        // Assert
        result.Success.Should().BeTrue();
        result.UpdateAvailable.Should().Be(expectedUpdateAvailable);
    }

    #endregion

    #region ApplyUpdate Tests

    [Fact]
    public void ApplyUpdate_MigratesResponsesByPromptId()
    {
        // Arrange
        var currentDoc = CreateFilledFormWithPrompts(new[]
        {
            ("prompt_1", "First Name", "John"),
            ("prompt_2", "Last Name", "Doe"),
            ("prompt_3", "Email", "john@example.com")
        });

        var newTemplate = CreateTemplateWithPrompts(new[]
        {
            ("prompt_1", "First Name"),
            ("prompt_2", "Last Name"),
            ("prompt_3", "Email"),
            ("prompt_4", "Phone")  // New prompt
        });

        // Act
        var result = _service.ApplyUpdate(currentDoc, newTemplate);

        // Assert
        result.MigratedPromptCount.Should().Be(3);
        result.NewPrompts.Should().HaveCount(1);
        result.NewPrompts[0].Id.Should().Be("prompt_4");
        result.OrphanedPrompts.Should().BeEmpty();

        // Verify responses migrated
        var section = result.MigratedDocument.Sections[0];
        section.Prompts.First(p => p.Id == "prompt_1").Response.Should().Be("John");
        section.Prompts.First(p => p.Id == "prompt_2").Response.Should().Be("Doe");
        section.Prompts.First(p => p.Id == "prompt_3").Response.Should().Be("john@example.com");
        section.Prompts.First(p => p.Id == "prompt_4").Response.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ApplyUpdate_TracksOrphanedPrompts()
    {
        // Arrange
        var currentDoc = CreateFilledFormWithPrompts(new[]
        {
            ("prompt_1", "First Name", "John"),
            ("prompt_2", "Old Field", "Old Value"),  // Will be removed
            ("prompt_3", "Another Old", "Another Value")  // Will be removed
        });

        var newTemplate = CreateTemplateWithPrompts(new[]
        {
            ("prompt_1", "First Name"),
            ("prompt_4", "New Field")
        });

        // Act
        var result = _service.ApplyUpdate(currentDoc, newTemplate);

        // Assert
        result.MigratedPromptCount.Should().Be(1);
        result.OrphanedPrompts.Should().HaveCount(2);
        result.OrphanedPrompts.Should().Contain(o => o.Id == "prompt_2" && o.Response == "Old Value");
        result.OrphanedPrompts.Should().Contain(o => o.Id == "prompt_3" && o.Response == "Another Value");
    }

    [Fact]
    public void ApplyUpdate_PreservesFilledFormMetadata()
    {
        // Arrange
        var currentDoc = CreateFilledFormWithPrompts(new[] { ("prompt_1", "Name", "John") });
        currentDoc.Metadata.FilledBy = "Test User";
        currentDoc.Metadata.FilledDate = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        var newTemplate = CreateTemplateWithPrompts(new[] { ("prompt_1", "Name") });

        // Act
        var result = _service.ApplyUpdate(currentDoc, newTemplate);

        // Assert
        result.MigratedDocument.Metadata.FilledBy.Should().Be("Test User");
        result.MigratedDocument.Metadata.FilledDate.Should().Be(new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc));
        result.MigratedDocument.DocumentType.Should().Be(DocumentType.FilledForm);
    }

    [Fact]
    public void ApplyUpdate_SetsModifiedTimestamp()
    {
        // Arrange
        var beforeTest = DateTime.UtcNow;
        var currentDoc = CreateFilledFormWithPrompts(new[] { ("prompt_1", "Name", "John") });
        var newTemplate = CreateTemplateWithPrompts(new[] { ("prompt_1", "Name") });

        // Act
        var result = _service.ApplyUpdate(currentDoc, newTemplate);

        // Assert
        result.MigratedDocument.Metadata.Modified.Should().BeOnOrAfter(beforeTest);
    }

    [Fact]
    public void ApplyUpdate_HandlesNestedSections()
    {
        // Arrange
        var currentDoc = CreateFilledFormWithNestedSections();
        var newTemplate = CreateTemplateWithNestedSections();

        // Act
        var result = _service.ApplyUpdate(currentDoc, newTemplate);

        // Assert
        result.MigratedPromptCount.Should().BeGreaterThan(0);
        result.MigratedDocument.Sections.Should().NotBeEmpty();
    }

    [Fact]
    public void ApplyUpdate_IgnoresEmptyResponses()
    {
        // Arrange
        var currentDoc = CreateFilledFormWithPrompts(new[]
        {
            ("prompt_1", "Name", "John"),
            ("prompt_2", "Empty", ""),  // Empty response
            ("prompt_3", "Null", null)  // Null response
        });

        var newTemplate = CreateTemplateWithPrompts(new[]
        {
            ("prompt_1", "Name"),
            ("prompt_4", "New")
        });

        // Act
        var result = _service.ApplyUpdate(currentDoc, newTemplate);

        // Assert
        // Empty and null responses should not be counted as orphaned
        result.OrphanedPrompts.Should().BeEmpty();
        result.MigratedPromptCount.Should().Be(1);
    }

    [Fact]
    public void ApplyUpdate_GeneratesSummary()
    {
        // Arrange
        var currentDoc = CreateFilledFormWithPrompts(new[]
        {
            ("prompt_1", "Name", "John"),
            ("prompt_2", "Old", "Value")
        });

        var newTemplate = CreateTemplateWithPrompts(new[]
        {
            ("prompt_1", "Name"),
            ("prompt_3", "New Field")
        });

        // Act
        var result = _service.ApplyUpdate(currentDoc, newTemplate);

        // Assert
        result.Summary.Should().Contain("migrated");
        result.Summary.Should().Contain("new");
        result.Summary.Should().Contain("removed");
    }

    [Fact]
    public void ApplyUpdate_AddsOrphanedResponsesToDescription()
    {
        // Arrange
        var currentDoc = CreateFilledFormWithPrompts(new[]
        {
            ("prompt_1", "Name", "John"),
            ("prompt_2", "Removed Field", "Important Value")
        });

        var newTemplate = CreateTemplateWithPrompts(new[]
        {
            ("prompt_1", "Name")
        });

        // Act
        var result = _service.ApplyUpdate(currentDoc, newTemplate);

        // Assert
        result.MigratedDocument.Metadata.Description.Should().Contain("Important Value");
        result.MigratedDocument.Metadata.Description.Should().Contain("Removed Field");
    }

    #endregion

    #region FetchTemplateAsync Tests

    [Fact]
    public async Task FetchTemplateAsync_ReturnsDeserializedTemplate()
    {
        // Arrange
        var template = CreateTemplate(templateVersion: "1.0");
        var json = _serializer.Serialize(template);
        SetupHttpResponse(HttpStatusCode.OK, json);

        // Act
        var result = await _service.FetchTemplateAsync("https://example.com/template.aprt");

        // Assert
        result.Should().NotBeNull();
        result.Metadata.TemplateVersion.Should().Be("1.0");
    }

    [Fact]
    public async Task FetchTemplateAsync_ThrowsOnHttpError()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.InternalServerError, "Error");

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => _service.FetchTemplateAsync("https://example.com/template.aprt"));
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
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
    }

    private static AprDocument CreateFilledForm(string? templateSourceUrl, string? templateVersion)
    {
        return new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata
            {
                Title = "Test Form",
                TemplateSourceUrl = templateSourceUrl,
                TemplateVersion = templateVersion
            },
            Sections = new List<Section>
            {
                new Section
                {
                    Id = "section_1",
                    Title = "Section 1",
                    Prompts = new List<Prompt>
                    {
                        new Prompt { Id = "prompt_1", Label = "Test Prompt", Response = "Test Response" }
                    }
                }
            }
        };
    }

    private static AprDocument CreateTemplate(string? templateVersion)
    {
        return new AprDocument
        {
            DocumentType = DocumentType.Template,
            Metadata = new Metadata
            {
                Title = "Test Template",
                TemplateVersion = templateVersion
            },
            Sections = new List<Section>
            {
                new Section
                {
                    Id = "section_1",
                    Title = "Section 1",
                    Prompts = new List<Prompt>
                    {
                        new Prompt { Id = "prompt_1", Label = "Test Prompt" }
                    }
                }
            }
        };
    }

    private static AprDocument CreateFilledFormWithPrompts((string id, string label, string? response)[] prompts)
    {
        return new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata
            {
                Title = "Test Filled Form",
                TemplateSourceUrl = "https://example.com/template.aprt",
                TemplateVersion = "1.0"
            },
            Sections = new List<Section>
            {
                new Section
                {
                    Id = "section_1",
                    Title = "Section 1",
                    Prompts = prompts.Select(p => new Prompt
                    {
                        Id = p.id,
                        Label = p.label,
                        Response = p.response
                    }).ToList()
                }
            }
        };
    }

    private static AprDocument CreateTemplateWithPrompts((string id, string label)[] prompts)
    {
        return new AprDocument
        {
            DocumentType = DocumentType.Template,
            Metadata = new Metadata
            {
                Title = "Test Template",
                TemplateVersion = "2.0"
            },
            Sections = new List<Section>
            {
                new Section
                {
                    Id = "section_1",
                    Title = "Section 1",
                    Prompts = prompts.Select(p => new Prompt
                    {
                        Id = p.id,
                        Label = p.label
                    }).ToList()
                }
            }
        };
    }

    private static AprDocument CreateFilledFormWithNestedSections()
    {
        return new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata
            {
                Title = "Nested Form",
                TemplateSourceUrl = "https://example.com/template.aprt",
                TemplateVersion = "1.0"
            },
            Sections = new List<Section>
            {
                new Section
                {
                    Id = "parent_section",
                    Title = "Parent Section",
                    Prompts = new List<Prompt>
                    {
                        new Prompt { Id = "parent_prompt", Label = "Parent Prompt", Response = "Parent Value" }
                    },
                    Sections = new List<Section>
                    {
                        new Section
                        {
                            Id = "child_section",
                            Title = "Child Section",
                            Prompts = new List<Prompt>
                            {
                                new Prompt { Id = "child_prompt", Label = "Child Prompt", Response = "Child Value" }
                            }
                        }
                    }
                }
            }
        };
    }

    private static AprDocument CreateTemplateWithNestedSections()
    {
        return new AprDocument
        {
            DocumentType = DocumentType.Template,
            Metadata = new Metadata
            {
                Title = "Nested Template",
                TemplateVersion = "2.0"
            },
            Sections = new List<Section>
            {
                new Section
                {
                    Id = "parent_section",
                    Title = "Parent Section",
                    Prompts = new List<Prompt>
                    {
                        new Prompt { Id = "parent_prompt", Label = "Parent Prompt" }
                    },
                    Sections = new List<Section>
                    {
                        new Section
                        {
                            Id = "child_section",
                            Title = "Child Section",
                            Prompts = new List<Prompt>
                            {
                                new Prompt { Id = "child_prompt", Label = "Child Prompt" },
                                new Prompt { Id = "new_child_prompt", Label = "New Child Prompt" }
                            }
                        }
                    }
                }
            }
        };
    }

    #endregion
}
