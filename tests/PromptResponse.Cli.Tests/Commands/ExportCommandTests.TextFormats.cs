using AwesomeAssertions;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands;

public partial class ExportCommandTests
{
    [Fact]
    public async Task ExecuteAsync_HtmlFormat_WritesAccessibleHtml()
    {
        var inputFile = await WriteDocumentAsync(CreateTestDocument());
        var outputFile = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}.html");
        try
        {
            (await _command.ExecuteAsync([inputFile, "--format=html", $"--output={outputFile}"])).Should().Be(0);
            var html = await File.ReadAllTextAsync(outputFile);
            html.Should().StartWith("<!DOCTYPE html>");
            html.Should().Contain("<html lang=\"en\">");
        }
        finally
        {
            File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithMixedCaseFormat_WritesRequestedFormat()
    {
        var inputFile = await WriteDocumentAsync(CreateTestDocument());
        var outputFile = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}.html");
        try
        {
            (await _command.ExecuteAsync([inputFile, "--format=HtMl", $"--output={outputFile}"])).Should().Be(0);
            (await File.ReadAllTextAsync(outputFile)).Should().StartWith("<!DOCTYPE html>");
        }
        finally
        {
            File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_TextAlias_WritesTextExport()
    {
        var inputFile = await WriteDocumentAsync(CreateTestDocument());
        var outputFile = Path.GetTempFileName();
        try
        {
            (await _command.ExecuteAsync([inputFile, "--format=TXT", $"--output={outputFile}"])).Should().Be(0);
            (await File.ReadAllTextAsync(outputFile)).Should().Contain("Responses: Test Form");
        }
        finally
        {
            File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_HtmlFillable_WritesInteractiveForm()
    {
        var inputFile = await WriteDocumentAsync(CreateTestDocument());
        var outputFile = Path.Combine(Path.GetTempPath(), $"form_{Guid.NewGuid():N}.html");
        try
        {
            (await _command.ExecuteAsync([inputFile, "--format=html", "--fillable", $"--output={outputFile}"])).Should().Be(0);
            var html = await File.ReadAllTextAsync(outputFile);
            html.Should().Contain("<form id=\"apr-form\"");
            html.Should().Contain("id=\"apr-download\"");
            html.Should().Contain("data-prompt-id=\"prompt1\"");
            html.Should().Contain(".aprf");
        }
        finally
        {
            File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

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
                                new Prompt { Id = "q1.revenue", Label = "Revenue", Response = "125000.00", Hints = new PromptHints { ExpectedDataType = "currency" } },
                                new Prompt { Id = "q1.note", Label = "Note", Response = "about 130k" },
                            ],
                        },
                    ],
                },
            ],
        };
        var inputFile = await WriteDocumentAsync(document);
        var outputFile = Path.GetTempFileName();
        try
        {
            (await _command.ExecuteAsync([inputFile, $"--format={format}", $"--output={outputFile}"])).Should().Be(0);
            var text = await File.ReadAllTextAsync(outputFile);
            text.Should().Contain("125000.00", "a table cell value must reach the export");
            text.Should().Contain("about 130k", "free text in a typed column is exported verbatim");
        }
        finally
        {
            File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithCsvFormat_ShouldReturnSuccess()
    {
        var inputFile = await WriteDocumentAsync(CreateTestDocument());
        try { (await _command.ExecuteAsync([inputFile, "--format=csv"])).Should().Be(0); }
        finally { File.Delete(inputFile); }
    }

    [Fact]
    public async Task ExecuteAsync_WithJsonFormat_ShouldReturnSuccess()
    {
        var inputFile = await WriteDocumentAsync(CreateTestDocument());
        try { (await _command.ExecuteAsync([inputFile, "--format=json"])).Should().Be(0); }
        finally { File.Delete(inputFile); }
    }

    [Fact]
    public async Task ExecuteAsync_WithTextFormat_ShouldReturnSuccess()
    {
        var inputFile = await WriteDocumentAsync(CreateTestDocument());
        try { (await _command.ExecuteAsync([inputFile, "--format=txt"])).Should().Be(0); }
        finally { File.Delete(inputFile); }
    }

    [Fact]
    public async Task ExecuteAsync_WithOutputFile_ShouldCreateFile()
    {
        var inputFile = await WriteDocumentAsync(CreateTestDocument());
        var outputFile = Path.GetTempFileName();
        File.Delete(outputFile);
        try
        {
            (await _command.ExecuteAsync([inputFile, "--format=csv", $"--output={outputFile}"])).Should().Be(0);
            File.Exists(outputFile).Should().BeTrue();
            var content = await File.ReadAllTextAsync(outputFile);
            content.Should().Contain("Section,Subsection,Prompt ID,Label,Response");
            content.Should().Contain("Test Section");
            content.Should().Contain("Test Question");
        }
        finally
        {
            File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_CsvExport_ShouldContainAllPrompts()
    {
        var inputFile = await WriteDocumentAsync(CreateDocumentWithMultiplePrompts());
        var outputFile = Path.GetTempFileName();
        File.Delete(outputFile);
        try
        {
            (await _command.ExecuteAsync([inputFile, "--format=csv", $"--output={outputFile}"])).Should().Be(0);
            var content = await File.ReadAllTextAsync(outputFile);
            content.Should().Contain("Question 1");
            content.Should().Contain("Question 2");
            content.Should().Contain("Answer 1");
        }
        finally
        {
            File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_JsonExport_ShouldContainResponsesArray()
    {
        var inputFile = await WriteDocumentAsync(CreateTestDocument());
        var outputFile = Path.GetTempFileName();
        File.Delete(outputFile);
        try
        {
            (await _command.ExecuteAsync([inputFile, "--format=json", $"--output={outputFile}"])).Should().Be(0);
            var content = await File.ReadAllTextAsync(outputFile);
            content.Should().Contain("\"responses\"");
            content.Should().Contain("\"title\"");
            content.Should().Contain("Test Form");
        }
        finally
        {
            File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }
}
