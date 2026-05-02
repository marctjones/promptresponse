using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Desktop.Services;
using Xunit;

namespace PromptResponse.Desktop.Tests.Services;

/// <summary>
/// Unit tests for FileService.
/// </summary>
/// <remarks>
/// These tests focus on testable aspects of FileService that don't require Avalonia dialogs:
/// CurrentFilePath management, extension-based DocumentType override, and serializer integration.
/// SaveFileAsync writes a real (empty) file via a mocked serializer to a per-test temp directory;
/// no fictitious paths are used.
/// </remarks>
public class FileServiceTests : IDisposable
{
    private readonly IAprSerializer _mockSerializer;
    private readonly string _tempDir;

    public FileServiceTests()
    {
        _mockSerializer = Substitute.For<IAprSerializer>();
        _tempDir = Path.Combine(Path.GetTempPath(), "promptresponse-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _mockSerializer
            .SerializeAsync(Arg.Any<AprDocument>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; ignore failures (e.g., file locks on Windows).
        }
    }

    private FileService CreateService() => new(_mockSerializer);

    private string PathFor(string fileName, string? subdir = null)
    {
        var dir = subdir != null ? Path.Combine(_tempDir, subdir) : _tempDir;
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, fileName);
    }

    [Fact]
    public void Constructor_ShouldInitializeWithNullCurrentFilePath()
    {
        var service = CreateService();
        service.CurrentFilePath.Should().BeNull();
    }

    [Fact]
    public void SetCurrentFilePath_ShouldUpdatePath()
    {
        var service = CreateService();
        var path = PathFor("document.aprt");

        service.SetCurrentFilePath(path);

        service.CurrentFilePath.Should().Be(path);
    }

    [Fact]
    public void ClearCurrentFilePath_ShouldResetPath()
    {
        var service = CreateService();
        service.SetCurrentFilePath(PathFor("document.aprt"));

        service.ClearCurrentFilePath();

        service.CurrentFilePath.Should().BeNull();
    }

    [Fact]
    public void SetCurrentFilePath_MultipleTimes_ShouldKeepLastPath()
    {
        var service = CreateService();
        var third = PathFor("third.aprf");

        service.SetCurrentFilePath(PathFor("first.apr"));
        service.SetCurrentFilePath(PathFor("second.aprt"));
        service.SetCurrentFilePath(third);

        service.CurrentFilePath.Should().Be(third);
    }

    [Fact]
    public async Task SaveFileAsync_WithAprtExtension_ShouldSetDocumentTypeToTemplate()
    {
        var service = CreateService();
        var document = CreateTestFilledForm();
        var filePath = PathFor("document.aprt");

        await service.SaveFileAsync(document, filePath);

        document.DocumentType.Should().Be(DocumentType.Template, ".aprt overrides DocumentType to Template");
        service.CurrentFilePath.Should().Be(filePath);
    }

    [Fact]
    public async Task SaveFileAsync_WithAprfExtension_ShouldSetDocumentTypeToFilledForm()
    {
        var service = CreateService();
        var document = CreateTestTemplate();
        var filePath = PathFor("document.aprf");

        await service.SaveFileAsync(document, filePath);

        document.DocumentType.Should().Be(DocumentType.FilledForm, ".aprf overrides DocumentType to FilledForm");
        service.CurrentFilePath.Should().Be(filePath);
    }

    [Fact]
    public async Task SaveFileAsync_WithAprExtension_ShouldKeepCurrentDocumentType()
    {
        var service = CreateService();
        var templateDoc = CreateTestTemplate();
        var filledDoc = CreateTestFilledForm();
        var templatePath = PathFor("template.apr");
        var filledPath = PathFor("filled.apr");

        await service.SaveFileAsync(templateDoc, templatePath);
        templateDoc.DocumentType.Should().Be(DocumentType.Template, ".apr does not change Template");

        await service.SaveFileAsync(filledDoc, filledPath);
        filledDoc.DocumentType.Should().Be(DocumentType.FilledForm, ".apr does not change FilledForm");
    }

    [Fact]
    public async Task SaveFileAsync_ShouldUpdateModifiedTimestamp()
    {
        var service = CreateService();
        var document = CreateTestTemplate();
        var originalModified = document.Metadata.Modified;
        var filePath = PathFor("document.aprt");

        await Task.Delay(50);
        await service.SaveFileAsync(document, filePath);

        document.Metadata.Modified.Should().NotBeNull();
        document.Metadata.Modified!.Value.Should().BeAfter(originalModified ?? DateTime.MinValue);
    }

    [Fact]
    public async Task SaveFileAsync_ShouldCallSerializerWithDocument()
    {
        var service = CreateService();
        var document = CreateTestTemplate();
        var filePath = PathFor("document.aprt");

        AprDocument? captured = null;
        _mockSerializer
            .SerializeAsync(Arg.Any<AprDocument>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(call => captured = call.Arg<AprDocument>());

        await service.SaveFileAsync(document, filePath);

        _ = _mockSerializer.Received(1).SerializeAsync(Arg.Any<AprDocument>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        captured.Should().BeSameAs(document);
    }

    [Fact]
    public async Task SaveFileAsync_ShouldSetCurrentFilePath()
    {
        var service = CreateService();
        var document = CreateTestTemplate();
        var filePath = PathFor("saved-document.aprt");

        await service.SaveFileAsync(document, filePath);

        service.CurrentFilePath.Should().Be(filePath);
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
        var service = CreateService();
        var document = expectedType == DocumentType.FilledForm
            ? CreateTestFilledForm()
            : CreateTestTemplate();
        // Flip the type so the extension-based override is exercised.
        document.DocumentType = expectedType == DocumentType.FilledForm
            ? DocumentType.Template
            : DocumentType.FilledForm;

        var filePath = PathFor($"document{extension}");

        await service.SaveFileAsync(document, filePath);

        document.DocumentType.Should().Be(expectedType, $"{extension} extension should set DocumentType to {expectedType}");
    }

    [Fact]
    public void SetCurrentFilePath_WithEmptyString_ShouldSetEmptyPath()
    {
        var service = CreateService();

        service.SetCurrentFilePath(string.Empty);

        service.CurrentFilePath.Should().Be(string.Empty);
    }

    [Fact]
    public void SetCurrentFilePath_ThenClear_ThenSet_ShouldWorkCorrectly()
    {
        var service = CreateService();

        service.SetCurrentFilePath(PathFor("first.apr"));
        service.CurrentFilePath.Should().NotBeNull();

        service.ClearCurrentFilePath();
        service.CurrentFilePath.Should().BeNull();

        var second = PathFor("second.aprt");
        service.SetCurrentFilePath(second);
        service.CurrentFilePath.Should().Be(second);
    }

    [Fact]
    public async Task SaveFileAsync_WithPathContainingSpaces_ShouldWork()
    {
        var service = CreateService();
        var document = CreateTestTemplate();
        var filePath = PathFor("document name.aprt", subdir: "path with spaces");

        await service.SaveFileAsync(document, filePath);

        service.CurrentFilePath.Should().Be(filePath);
    }

    [Fact]
    public async Task SaveFileAsync_WithUnicodeCharactersInPath_ShouldWork()
    {
        var service = CreateService();
        var document = CreateTestTemplate();
        var filePath = PathFor("formulaire-éàü-日本語.aprt", subdir: "Documents-éàü");

        await service.SaveFileAsync(document, filePath);

        service.CurrentFilePath.Should().Be(filePath);
    }

    private static AprDocument CreateTestTemplate() =>
        new()
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
                new() {
                    Id = "section_1",
                    Title = "Test Section",
                    Prompts = new List<Prompt> { new() { Id = "prompt_1", Label = "Test Prompt" } },
                    Sections = new List<Section>()
                }
            }
        };

    private static AprDocument CreateTestFilledForm() =>
        new()
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
                new() {
                    Id = "section_1",
                    Title = "Test Section",
                    Prompts = new List<Prompt> { new() { Id = "prompt_1", Label = "Test Prompt", Response = "Test Response" } },
                    Sections = new List<Section>()
                }
            }
        };
}
