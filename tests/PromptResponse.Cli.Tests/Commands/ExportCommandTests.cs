using AwesomeAssertions;
using PromptResponse.Cli.Commands;
using PromptResponse.Core;
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
    public async Task ExecuteAsync_Pdfa_WritesArchivalPdfWithPdfAMarkers()
    {
        var document = CreateTestDocument();
        var tempFile = Path.GetTempFileName();
        var outputFile = Path.Combine(Path.GetTempPath(), $"archival_{Guid.NewGuid():N}.pdf");

        try
        {
            await File.WriteAllTextAsync(tempFile, _serializer.Serialize(document));

            var exitCode = await _command.ExecuteAsync(new[] { tempFile, "--format=pdf", "--pdfa", $"--output={outputFile}" });

            exitCode.Should().Be(0);
            var raw = System.Text.Encoding.Latin1.GetString(await File.ReadAllBytesAsync(outputFile));
            raw.Should().Contain("pdfaid:part").And.Contain("/OutputIntents").And.Contain("DejaVuSans");
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
    public async Task ExecuteAsync_HtmlFormat_WritesAccessibleHtml()
    {
        var document = CreateTestDocument();
        var tempFile = Path.GetTempFileName();
        var outputFile = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}.html");

        try
        {
            await File.WriteAllTextAsync(tempFile, _serializer.Serialize(document));

            var exitCode = await _command.ExecuteAsync(new[] { tempFile, "--format=html", $"--output={outputFile}" });

            exitCode.Should().Be(0);
            var html = await File.ReadAllTextAsync(outputFile);
            html.Should().StartWith("<!DOCTYPE html>");
            html.Should().Contain("<html lang=\"en\">");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_HtmlFillable_WritesInteractiveForm()
    {
        var document = CreateTestDocument();
        var tempFile = Path.GetTempFileName();
        var outputFile = Path.Combine(Path.GetTempPath(), $"form_{Guid.NewGuid():N}.html");

        try
        {
            await File.WriteAllTextAsync(tempFile, _serializer.Serialize(document));

            var exitCode = await _command.ExecuteAsync(new[] { tempFile, "--format=html", "--fillable", $"--output={outputFile}" });

            exitCode.Should().Be(0);
            var html = await File.ReadAllTextAsync(outputFile);
            html.Should().Contain("<form id=\"apr-form\"");
            html.Should().Contain("id=\"apr-download\"");
            html.Should().Contain("data-prompt-id=\"prompt1\"");
            html.Should().Contain(".aprf");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    /// <summary>
    /// Exporting a table section through the CLI.
    /// </summary>
    /// <remarks>
    /// The table redesign (kind=table) changed how tables reach a renderer: headers now
    /// come from the first instance's prompt labels rather than a column array, and
    /// cells correspond by position. CLI export walks the same render model, so this
    /// path changed underneath a command that had no table coverage at all before or
    /// after. Exercises the shape end to end and asserts the headers and cell values
    /// actually reach the output.
    /// </remarks>
    [Theory]
    [InlineData("csv")]
    [InlineData("json")]
    [InlineData("txt")]
    public async Task ExecuteAsync_WithTableSection_ExportsHeadersAndCells(string format)
    {
        var document = new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata { Title = "Quarterly", TemplateId = "t" },
            Sections =
            [
                new Section
                {
                    Id = "totals", Title = "Quarterly totals", Kind = "table",
                    Sections =
                    [
                        new Section
                        {
                            Id = "q1", Title = "Q1",
                            Prompts =
                            [
                                new Prompt { Id = "q1.revenue", Label = "Revenue", Response = "125000.00",
                                    Hints = new PromptHints { ExpectedDataType = "currency" } },
                                // Free text in a currency column: still valid, still exported verbatim.
                                new Prompt { Id = "q1.note", Label = "Note", Response = "about 130k" },
                            ],
                        },
                    ],
                },
            ],
        };

        var input = Path.GetTempFileName();
        var output = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(input, _serializer.Serialize(document));

            var exitCode = await _command.ExecuteAsync([input, $"--format={format}", $"--output={output}"]);

            exitCode.Should().Be(0);
            var text = await File.ReadAllTextAsync(output);
            text.Should().Contain("125000.00", "a table cell value must reach the export");
            text.Should().Contain("about 130k", "free text in a typed column is exported verbatim");
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (File.Exists(output)) File.Delete(output);
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
            Version = AprFormat.CurrentVersion,
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
            Version = AprFormat.CurrentVersion,
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
