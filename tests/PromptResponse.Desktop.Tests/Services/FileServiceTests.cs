using FluentAssertions;
using Moq;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Desktop.Services;
using Xunit;

namespace PromptResponse.Desktop.Tests.Services;

/// <summary>
/// Unit tests for FileService.
/// </summary>
/// <remarks>
/// These tests focus on the testable aspects of FileService that don't require Avalonia dialogs:
/// - CurrentFilePath management (Set, Clear, Get)
/// - Extension-based DocumentType override logic in SaveFileAsync
/// - Serialization integration
///
/// Tests for OpenFileAsync and SaveFileAsAsync require Avalonia StorageProvider dialogs
/// and are better suited for integration tests with UI automation.
/// </remarks>
public class FileServiceTests
{
    private readonly Mock<IAprSerializer> _mockSerializer;

    public FileServiceTests()
    {
        _mockSerializer = new Mock<IAprSerializer>();
    }

    private FileService CreateService()
    {
        return new FileService(_mockSerializer.Object);
    }

    [Fact]
    public void Constructor_ShouldInitializeWithNullCurrentFilePath()
    {
        // Arrange & Act
        var service = CreateService();

        // Assert
        service.CurrentFilePath.Should().BeNull("CurrentFilePath should be null initially");
    }

    [Fact]
    public void SetCurrentFilePath_ShouldUpdatePath()
    {
        // Arrange
        var service = CreateService();
        const string testPath = "/test/document.aprt";

        // Act
        service.SetCurrentFilePath(testPath);

        // Assert
        service.CurrentFilePath.Should().Be(testPath, "CurrentFilePath should be set to the provided path");
    }

    [Fact]
    public void ClearCurrentFilePath_ShouldResetPath()
    {
        // Arrange
        var service = CreateService();
        service.SetCurrentFilePath("/test/document.aprt");

        // Act
        service.ClearCurrentFilePath();

        // Assert
        service.CurrentFilePath.Should().BeNull("CurrentFilePath should be null after clearing");
    }

    [Fact]
    public void SetCurrentFilePath_MultipleTimes_ShouldKeepLastPath()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.SetCurrentFilePath("/test/first.apr");
        service.SetCurrentFilePath("/test/second.aprt");
        service.SetCurrentFilePath("/test/third.aprf");

        // Assert
        service.CurrentFilePath.Should().Be("/test/third.aprf", "CurrentFilePath should be the last set path");
    }

    [Fact]
    public async Task SaveFileAsync_WithAprtExtension_ShouldSetDocumentTypeToTemplate()
    {
        // Arrange
        var service = CreateService();
        var document = CreateTestFilledForm(); // Start with filled form
        const string filePath = "/test/document.aprt";

        _mockSerializer.Setup(s => s.SerializeAsync(It.IsAny<AprDocument>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await service.SaveFileAsync(document, filePath);

        // Assert
        document.DocumentType.Should().Be(DocumentType.Template, ".aprt extension should override DocumentType to Template");
        service.CurrentFilePath.Should().Be(filePath, "CurrentFilePath should be set after save");
    }

    [Fact]
    public async Task SaveFileAsync_WithAprfExtension_ShouldSetDocumentTypeToFilledForm()
    {
        // Arrange
        var service = CreateService();
        var document = CreateTestTemplate(); // Start with template
        const string filePath = "/test/document.aprf";

        _mockSerializer.Setup(s => s.SerializeAsync(It.IsAny<AprDocument>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await service.SaveFileAsync(document, filePath);

        // Assert
        document.DocumentType.Should().Be(DocumentType.FilledForm, ".aprf extension should override DocumentType to FilledForm");
        service.CurrentFilePath.Should().Be(filePath, "CurrentFilePath should be set after save");
    }

    [Fact]
    public async Task SaveFileAsync_WithAprExtension_ShouldKeepCurrentDocumentType()
    {
        // Arrange
        var service = CreateService();
        var templateDoc = CreateTestTemplate();
        var filledDoc = CreateTestFilledForm();
        const string templatePath = "/test/template.apr";
        const string filledPath = "/test/filled.apr";

        _mockSerializer.Setup(s => s.SerializeAsync(It.IsAny<AprDocument>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act & Assert for template
        await service.SaveFileAsync(templateDoc, templatePath);
        templateDoc.DocumentType.Should().Be(DocumentType.Template, ".apr extension should not change Template type");

        // Act & Assert for filled form
        await service.SaveFileAsync(filledDoc, filledPath);
        filledDoc.DocumentType.Should().Be(DocumentType.FilledForm, ".apr extension should not change FilledForm type");
    }

    [Fact]
    public async Task SaveFileAsync_ShouldUpdateModifiedTimestamp()
    {
        // Arrange
        var service = CreateService();
        var document = CreateTestTemplate();
        var originalModified = document.Metadata.Modified;
        const string filePath = "/test/document.aprt";

        _mockSerializer.Setup(s => s.SerializeAsync(It.IsAny<AprDocument>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Wait a bit to ensure timestamp difference
        await Task.Delay(50);

        // Act
        await service.SaveFileAsync(document, filePath);

        // Assert
        document.Metadata.Modified.Should().NotBeNull("Modified timestamp should be set");
        document.Metadata.Modified!.Value.Should().BeAfter(originalModified ?? DateTime.MinValue, "Modified timestamp should be updated on save");
    }

    [Fact]
    public async Task SaveFileAsync_ShouldCallSerializerWithDocument()
    {
        // Arrange
        var service = CreateService();
        var document = CreateTestTemplate();
        const string filePath = "/test/document.aprt";

        AprDocument? capturedDocument = null;
        _mockSerializer.Setup(s => s.SerializeAsync(It.IsAny<AprDocument>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<AprDocument, Stream, CancellationToken>((doc, _, _) => capturedDocument = doc)
            .Returns(Task.CompletedTask);

        // Act
        await service.SaveFileAsync(document, filePath);

        // Assert
        _mockSerializer.Verify(s => s.SerializeAsync(It.IsAny<AprDocument>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        capturedDocument.Should().BeSameAs(document, "serializer should be called with the provided document");
    }

    [Fact]
    public async Task SaveFileAsync_ShouldSetCurrentFilePath()
    {
        // Arrange
        var service = CreateService();
        var document = CreateTestTemplate();
        const string filePath = "/test/saved-document.aprt";

        _mockSerializer.Setup(s => s.SerializeAsync(It.IsAny<AprDocument>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await service.SaveFileAsync(document, filePath);

        // Assert
        service.CurrentFilePath.Should().Be(filePath, "CurrentFilePath should be set after saving");
    }

    [Theory]
    [InlineData(".aprt", DocumentType.Template)]
    [InlineData(".APRT", DocumentType.Template)]
    [InlineData(".Aprt", DocumentType.Template)]
    [InlineData(".aprf", DocumentType.FilledForm)]
    [InlineData(".APRF", DocumentType.FilledForm)]
    [InlineData(".Aprf", DocumentType.FilledForm)]
    public async Task SaveFileAsync_WithVariousExtensionCases_ShouldSetCorrectDocumentType(string extension, DocumentType expectedType)
    {
        // Arrange
        var service = CreateService();
        var document = CreateTestTemplate();
        if (expectedType == DocumentType.FilledForm)
        {
            document = CreateTestFilledForm();
            document.DocumentType = DocumentType.Template; // Override to test conversion
        }
        else
        {
            document.DocumentType = DocumentType.FilledForm; // Override to test conversion
        }
        var filePath = $"/test/document{extension}";

        _mockSerializer.Setup(s => s.SerializeAsync(It.IsAny<AprDocument>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await service.SaveFileAsync(document, filePath);

        // Assert
        document.DocumentType.Should().Be(expectedType, $"{extension} extension should set DocumentType to {expectedType}");
    }

    [Fact]
    public void SetCurrentFilePath_WithEmptyString_ShouldSetEmptyPath()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.SetCurrentFilePath(string.Empty);

        // Assert
        service.CurrentFilePath.Should().Be(string.Empty, "should accept empty string as path");
    }

    [Fact]
    public void SetCurrentFilePath_ThenClear_ThenSet_ShouldWorkCorrectly()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        service.SetCurrentFilePath("/first/path.apr");
        service.CurrentFilePath.Should().Be("/first/path.apr");

        service.ClearCurrentFilePath();
        service.CurrentFilePath.Should().BeNull();

        service.SetCurrentFilePath("/second/path.aprt");
        service.CurrentFilePath.Should().Be("/second/path.aprt");
    }

    [Fact]
    public async Task SaveFileAsync_WithPathContainingSpaces_ShouldWork()
    {
        // Arrange
        var service = CreateService();
        var document = CreateTestTemplate();
        const string filePath = "/test/path with spaces/document name.aprt";

        _mockSerializer.Setup(s => s.SerializeAsync(It.IsAny<AprDocument>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await service.SaveFileAsync(document, filePath);

        // Assert
        service.CurrentFilePath.Should().Be(filePath, "should handle paths with spaces");
    }

    [Fact]
    public async Task SaveFileAsync_WithUnicodeCharactersInPath_ShouldWork()
    {
        // Arrange
        var service = CreateService();
        var document = CreateTestTemplate();
        const string filePath = "/test/Documents/formulaire.aprt";

        _mockSerializer.Setup(s => s.SerializeAsync(It.IsAny<AprDocument>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await service.SaveFileAsync(document, filePath);

        // Assert
        service.CurrentFilePath.Should().Be(filePath, "should handle Unicode characters in path");
    }

    private static AprDocument CreateTestTemplate()
    {
        return new AprDocument
        {
            DocumentType = DocumentType.Template,
            Metadata = new Metadata
            {
                Title = "Test Template",
                Description = "Test template description",
                TemplateVersion = "1.0.0",
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow
            },
            Sections = new List<Section>
            {
                new Section
                {
                    Id = "section_1",
                    Title = "Test Section",
                    Prompts = new List<Prompt>
                    {
                        new Prompt
                        {
                            Id = "prompt_1",
                            Label = "Test Prompt"
                        }
                    },
                    Sections = new List<Section>()
                }
            }
        };
    }

    private static AprDocument CreateTestFilledForm()
    {
        return new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata
            {
                Title = "Test Filled Form",
                Description = "Test form description",
                TemplateVersion = "1.0.0",
                TemplateId = "template-123",
                FilledBy = "TestUser",
                FilledDate = DateTime.UtcNow,
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow
            },
            Sections = new List<Section>
            {
                new Section
                {
                    Id = "section_1",
                    Title = "Test Section",
                    Prompts = new List<Prompt>
                    {
                        new Prompt
                        {
                            Id = "prompt_1",
                            Label = "Test Prompt",
                            Response = "Test Response"
                        }
                    },
                    Sections = new List<Section>()
                }
            }
        };
    }
}
