using AwesomeAssertions;
using PromptResponse.Cli.Commands;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using PromptResponse.Rendering.Pdf;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands;

/// <summary>
/// Unit tests for ImportCommand (fillable PDF → APR template).
/// </summary>
public class ImportCommandTests
{
    private readonly AprJsonSerializer _serializer = new();
    private readonly ImportCommand _command;

    public ImportCommandTests()
    {
        _command = new ImportCommand(_serializer, new DocumentValidator());
    }

    private static AprDocument SourceForm() => new()
    {
        DocumentType = DocumentType.Template,
        Metadata = new Metadata { Title = "Sign-up" },
        Sections =
        [
            new Section
            {
                Id = "s1", Title = "Details",
                Prompts =
                [
                    new Prompt { Id = "full_name", Label = "Full Name", Hints = new PromptHints { ExpectedDataType = "text" } },
                    new Prompt { Id = "agree", Label = "I agree", Hints = new PromptHints { ExpectedDataType = "boolean" } },
                ],
            },
        ],
    };

    [Fact]
    public async Task ExecuteAsync_NoArgs_ReturnsError()
    {
        (await _command.ExecuteAsync(Array.Empty<string>())).Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_MissingFile_ReturnsError()
    {
        (await _command.ExecuteAsync(new[] { "/nope/missing.pdf" })).Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_FillablePdf_WritesValidTemplate()
    {
        var pdf = Path.Combine(Path.GetTempPath(), $"in_{Guid.NewGuid():N}.pdf");
        var outPath = Path.Combine(Path.GetTempPath(), $"out_{Guid.NewGuid():N}.aprt");
        await File.WriteAllBytesAsync(pdf, new FillablePdfDocumentRenderer().RenderToBytes(SourceForm()));

        try
        {
            var exit = await _command.ExecuteAsync(new[] { pdf, $"--output={outPath}", "--title=Recovered" });

            exit.Should().Be(0);
            File.Exists(outPath).Should().BeTrue();

            var doc = _serializer.Deserialize(await File.ReadAllTextAsync(outPath));
            doc.DocumentType.Should().Be(DocumentType.Template);
            doc.Metadata.Title.Should().Be("Recovered");
            doc.Sections.SelectMany(s => s.Prompts).Select(p => p.Label)
                .Should().Contain(["Full Name", "I agree"]);
            new DocumentValidator().Validate(doc).IsValid.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(pdf)) File.Delete(pdf);
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Report_PrintsQualityVerdictAndBreakdown()
    {
        var pdf = Path.Combine(Path.GetTempPath(), $"in_{Guid.NewGuid():N}.pdf");
        var outPath = Path.Combine(Path.GetTempPath(), $"out_{Guid.NewGuid():N}.aprt");
        await File.WriteAllBytesAsync(pdf, new FillablePdfDocumentRenderer().RenderToBytes(SourceForm()));

        var original = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            var exit = await _command.ExecuteAsync(new[] { pdf, $"--output={outPath}", "--report" });

            exit.Should().Be(0);
            var output = sw.ToString();
            output.Should().Contain("Quality:");
            output.Should().Contain("Import quality report");
            output.Should().Contain("Recommendation:");
        }
        finally
        {
            Console.SetOut(original);
            if (File.Exists(pdf)) File.Delete(pdf);
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Fact]
    public async Task ExecuteAsync_FlatPdf_ReturnsError()
    {
        // A flat PDF has no AcroForm — the command should fail cleanly.
        var pdf = Path.Combine(Path.GetTempPath(), $"flat_{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(pdf, new PdfDocumentRenderer().RenderToBytes(SourceForm()));

        try
        {
            (await _command.ExecuteAsync(new[] { pdf })).Should().Be(1);
        }
        finally
        {
            if (File.Exists(pdf)) File.Delete(pdf);
        }
    }
}
