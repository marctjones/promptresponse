using AwesomeAssertions;
using PromptResponse.Cli.Commands;
using PromptResponse.Cli.Tests.Fixtures;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands;

/// <summary>
/// Unit tests for InfoCommand.
/// </summary>
public class InfoCommandTests : IDisposable
{
    private readonly IAprSerializer _serializer;
    private readonly InfoCommand _command;
    private readonly TempFileHelper _tempHelper;

    public InfoCommandTests()
    {
        _serializer = new AprJsonSerializer();
        _command = new InfoCommand(_serializer);
        _tempHelper = new TempFileHelper(_serializer);
    }

    [Fact]
    public async Task ExecuteAsync_NoArguments_ReturnsError()
    {
        // Act
        var result = await _command.ExecuteAsync(Array.Empty<string>());

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_FileNotFound_ReturnsError()
    {
        // Act
        var result = await _command.ExecuteAsync(new[] { "/nonexistent/path/file.apr" });

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ValidTemplate_ReturnsSuccess()
    {
        // Arrange
        var templatePath = _tempHelper.CreateTemplateFile();

        // Act
        var result = await _command.ExecuteAsync(new[] { templatePath });

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ValidFilledForm_ReturnsSuccess()
    {
        // Arrange
        var filledFormPath = _tempHelper.CreateFilledFormFile();

        // Act
        var result = await _command.ExecuteAsync(new[] { filledFormPath });

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_MalformedJson_ReturnsError()
    {
        // Arrange
        var filePath = _tempHelper.CreateFileWithContent("{ invalid json");

        // Act
        var result = await _command.ExecuteAsync(new[] { filePath });

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ComplexTemplate_ReturnsSuccess()
    {
        // Arrange
        var templatePath = _tempHelper.CreateTempFile(TestDocumentFactory.CreateComplexTemplate());

        // Act
        var result = await _command.ExecuteAsync(new[] { templatePath });

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_PartiallyFilledForm_ReturnsSuccess()
    {
        // Arrange
        var partialPath = _tempHelper.CreateTempFile(TestDocumentFactory.CreatePartiallyFilledForm());

        // Act
        var result = await _command.ExecuteAsync(new[] { partialPath });

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithExtraArgs_IgnoresExtra()
    {
        // Arrange
        var templatePath = _tempHelper.CreateTemplateFile();

        // Act
        var result = await _command.ExecuteAsync(new[] { templatePath, "extra", "args" });

        // Assert
        result.Should().Be(0);
    }

    [Theory]
    [InlineData("file.apr")]
    [InlineData("file.aprt")]
    [InlineData("file.aprf")]
    public async Task ExecuteAsync_AllExtensions_Succeed(string fileName)
    {
        // Arrange
        var filePath = _tempHelper.CreateTempFile(TestDocumentFactory.CreateMinimalTemplate(), fileName);

        // Act
        var result = await _command.ExecuteAsync(new[] { filePath });

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesEmptyTitle()
    {
        // Arrange
        var doc = TestDocumentFactory.CreateMinimalTemplate();
        doc.Metadata.Title = "";
        var filePath = _tempHelper.CreateTempFile(doc);

        // Act
        var result = await _command.ExecuteAsync(new[] { filePath });

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesNullAuthor()
    {
        // Arrange
        var doc = TestDocumentFactory.CreateMinimalTemplate();
        doc.Metadata.Author = null;
        var filePath = _tempHelper.CreateTempFile(doc);

        // Act
        var result = await _command.ExecuteAsync(new[] { filePath });

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesMissingMetadata()
    {
        // Arrange
        var doc = TestDocumentFactory.CreateMinimalTemplate();
        doc.Metadata.Created = null;
        doc.Metadata.Modified = null;
        var filePath = _tempHelper.CreateTempFile(doc);

        // Act
        var result = await _command.ExecuteAsync(new[] { filePath });

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_FilledFormShowsCompletionPercentage()
    {
        // Arrange
        var filledPath = _tempHelper.CreateFilledFormFile();

        // Act & Assert - should not throw
        var result = await _command.ExecuteAsync(new[] { filledPath });
        result.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_TemplateWithoutMetadata_Succeeds()
    {
        // Arrange
        var doc = TestDocumentFactory.CreateMinimalTemplate();
        doc.Metadata.TemplateId = null;
        doc.Metadata.TemplateVersion = null;
        var filePath = _tempHelper.CreateTempFile(doc);

        // Act
        var result = await _command.ExecuteAsync(new[] { filePath });

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_DeepNesting_Succeeds()
    {
        // Arrange - create a deeply nested document
        var doc = TestDocumentFactory.CreateComplexTemplate();
        var filePath = _tempHelper.CreateTempFile(doc);

        // Act
        var result = await _command.ExecuteAsync(new[] { filePath });

        // Assert
        result.Should().Be(0);
    }

    public void Dispose()
    {
        _tempHelper?.Dispose();
    }
}
