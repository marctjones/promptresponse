using AwesomeAssertions;
using PromptResponse.Cli.Commands;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands;

/// <summary>
/// Unit tests for ExportCommand.
/// </summary>
public class ExportCommandTests
{
    private readonly AprJsonSerializer _serializer;
    private readonly ExportCommand _command;

    public ExportCommandTests()
    {
        _serializer = new AprJsonSerializer();
        _command = new ExportCommand(_serializer);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoArgs_ShouldReturnError()
    {
        // Arrange
        var args = Array.Empty<string>();

        // Act
        var exitCode = await _command.ExecuteAsync(args);

        // Assert
        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentFile_ShouldReturnError()
    {
        // Arrange
        var args = new[] { "nonexistent.apr" };

        // Act
        var exitCode = await _command.ExecuteAsync(args);

        // Assert
        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnsupportedFormat_ShouldReturnError()
    {
        // Arrange
        var document = CreateTestDocument();
        var tempFile = Path.GetTempFileName();

        try
        {
            var json = _serializer.Serialize(document);
            await File.WriteAllTextAsync(tempFile, json);

            var args = new[] { tempFile, "--format=xml" };

            // Act
            var exitCode = await _command.ExecuteAsync(args);

            // Assert
            exitCode.Should().Be(1);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_PdfFormat_WritesValidPdfFile()
    {
        // Arrange
        var document = CreateTestDocument();
        var tempFile = Path.GetTempFileName();
        var outputFile = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}.pdf");

        try
        {
            await File.WriteAllTextAsync(tempFile, _serializer.Serialize(document));

            var args = new[] { tempFile, "--format=pdf", $"--output={outputFile}" };

            // Act
            var exitCode = await _command.ExecuteAsync(args);

            // Assert
            exitCode.Should().Be(0);
            File.Exists(outputFile).Should().BeTrue();
            var bytes = await File.ReadAllBytesAsync(outputFile);
            bytes.Should().NotBeEmpty();
            System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_PdfFormat_WithoutOutput_ShouldReturnError()
    {
        // Arrange — PDF is binary and must go to a file, not stdout.
        var document = CreateTestDocument();
        var tempFile = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(tempFile, _serializer.Serialize(document));

            var args = new[] { tempFile, "--format=pdf" };

            // Act
            var exitCode = await _command.ExecuteAsync(args);

            // Assert
            exitCode.Should().Be(1);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_PdfFormat_Fillable_WritesAcroFormPdf()
    {
        // Arrange
        var document = CreateTestDocument();
        var tempFile = Path.GetTempFileName();
        var outputFile = Path.Combine(Path.GetTempPath(), $"form_{Guid.NewGuid():N}.pdf");

        try
        {
            await File.WriteAllTextAsync(tempFile, _serializer.Serialize(document));

            var args = new[] { tempFile, "--format=pdf", "--fillable", $"--output={outputFile}" };

            // Act
            var exitCode = await _command.ExecuteAsync(args);

            // Assert — valid PDF that declares an interactive AcroForm.
            exitCode.Should().Be(0);
            var bytes = await File.ReadAllBytesAsync(outputFile);
            System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
            System.Text.Encoding.Latin1.GetString(bytes).Should().Contain("/AcroForm");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithCsvFormat_ShouldReturnSuccess()
    {
        // Arrange
        var document = CreateTestDocument();
        var tempFile = Path.GetTempFileName();

        try
        {
            var json = _serializer.Serialize(document);
            await File.WriteAllTextAsync(tempFile, json);

            var args = new[] { tempFile, "--format=csv" };

            // Act
            var exitCode = await _command.ExecuteAsync(args);

            // Assert
            exitCode.Should().Be(0);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithJsonFormat_ShouldReturnSuccess()
    {
        // Arrange
        var document = CreateTestDocument();
        var tempFile = Path.GetTempFileName();

        try
        {
            var json = _serializer.Serialize(document);
            await File.WriteAllTextAsync(tempFile, json);

            var args = new[] { tempFile, "--format=json" };

            // Act
            var exitCode = await _command.ExecuteAsync(args);

            // Assert
            exitCode.Should().Be(0);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithTextFormat_ShouldReturnSuccess()
    {
        // Arrange
        var document = CreateTestDocument();
        var tempFile = Path.GetTempFileName();

        try
        {
            var json = _serializer.Serialize(document);
            await File.WriteAllTextAsync(tempFile, json);

            var args = new[] { tempFile, "--format=txt" };

            // Act
            var exitCode = await _command.ExecuteAsync(args);

            // Assert
            exitCode.Should().Be(0);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithOutputFile_ShouldCreateFile()
    {
        // Arrange
        var document = CreateTestDocument();
        var inputFile = Path.GetTempFileName();
        var outputFile = Path.GetTempFileName();

        try
        {
            var json = _serializer.Serialize(document);
            await File.WriteAllTextAsync(inputFile, json);

            // Delete output file so we can test creation
            File.Delete(outputFile);

            var args = new[] { inputFile, "--format=csv", $"--output={outputFile}" };

            // Act
            var exitCode = await _command.ExecuteAsync(args);

            // Assert
            exitCode.Should().Be(0);
            File.Exists(outputFile).Should().BeTrue();

            var content = await File.ReadAllTextAsync(outputFile);
            content.Should().Contain("Section,Subsection,Prompt ID,Label,Response");
            content.Should().Contain("Test Section");
            content.Should().Contain("Test Question");
        }
        finally
        {
            if (File.Exists(inputFile)) File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_CsvExport_ShouldContainAllPrompts()
    {
        // Arrange
        var document = CreateDocumentWithMultiplePrompts();
        var inputFile = Path.GetTempFileName();
        var outputFile = Path.GetTempFileName();

        try
        {
            var json = _serializer.Serialize(document);
            await File.WriteAllTextAsync(inputFile, json);

            File.Delete(outputFile);

            var args = new[] { inputFile, "--format=csv", $"--output={outputFile}" };

            // Act
            var exitCode = await _command.ExecuteAsync(args);

            // Assert
            exitCode.Should().Be(0);

            var content = await File.ReadAllTextAsync(outputFile);
            content.Should().Contain("Question 1");
            content.Should().Contain("Question 2");
            content.Should().Contain("Answer 1");
        }
        finally
        {
            if (File.Exists(inputFile)) File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_JsonExport_ShouldContainResponsesArray()
    {
        // Arrange
        var document = CreateTestDocument();
        var inputFile = Path.GetTempFileName();
        var outputFile = Path.GetTempFileName();

        try
        {
            var json = _serializer.Serialize(document);
            await File.WriteAllTextAsync(inputFile, json);

            File.Delete(outputFile);

            var args = new[] { inputFile, "--format=json", $"--output={outputFile}" };

            // Act
            var exitCode = await _command.ExecuteAsync(args);

            // Assert
            exitCode.Should().Be(0);

            var content = await File.ReadAllTextAsync(outputFile);
            content.Should().Contain("\"responses\"");
            content.Should().Contain("\"title\"");
            content.Should().Contain("Test Form");
        }
        finally
        {
            if (File.Exists(inputFile)) File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    private AprDocument CreateTestDocument()
    {
        return new AprDocument
        {
            Version = "1.0",
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata
            {
                Title = "Test Form",
                TemplateId = "test-v1"
            },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "section1",
                    Title = "Test Section",
                    Prompts = new List<Prompt>
                    {
                        new()
                        {
                            Id = "prompt1",
                            Label = "Test Question",
                            Response = "Test Answer",
                            Hints = new PromptHints
                            {
                                ExpectedDataType = "text"
                            }
                        }
                    }
                }
            }
        };
    }

    private AprDocument CreateDocumentWithMultiplePrompts()
    {
        return new AprDocument
        {
            Version = "1.0",
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata
            {
                Title = "Multi-Prompt Form",
                TemplateId = "multi-v1"
            },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "section1",
                    Title = "Section 1",
                    Prompts = new List<Prompt>
                    {
                        new()
                        {
                            Id = "prompt1",
                            Label = "Question 1",
                            Response = "Answer 1"
                        },
                        new()
                        {
                            Id = "prompt2",
                            Label = "Question 2",
                            Response = "Answer 2"
                        }
                    }
                }
            }
        };
    }
}
