using AwesomeAssertions;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands;

public partial class ExportCommandTests
{
    [Fact]
    public async Task ExecuteAsync_PdfFormat_WritesValidPdfFile()
    {
        var inputFile = await WriteDocumentAsync(CreateTestDocument());
        var outputFile = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid():N}.pdf");
        try
        {
            (await _command.ExecuteAsync([inputFile, "--format=pdf", $"--output={outputFile}"])).Should().Be(0);
            File.Exists(outputFile).Should().BeTrue();
            var bytes = await File.ReadAllBytesAsync(outputFile);
            bytes.Should().NotBeEmpty();
            System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        }
        finally
        {
            File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Pdfa_WritesArchivalPdfWithPdfAMarkers()
    {
        var inputFile = await WriteDocumentAsync(CreateTestDocument());
        var outputFile = Path.Combine(Path.GetTempPath(), $"archival_{Guid.NewGuid():N}.pdf");
        try
        {
            (await _command.ExecuteAsync([inputFile, "--format=pdf", "--pdfa", $"--output={outputFile}"])).Should().Be(0);
            var raw = System.Text.Encoding.Latin1.GetString(await File.ReadAllBytesAsync(outputFile));
            raw.Should().Contain("pdfaid:part").And.Contain("/OutputIntents").And.Contain("DejaVuSans");
        }
        finally
        {
            File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_PdfFormat_Fillable_WritesAcroFormPdf()
    {
        var inputFile = await WriteDocumentAsync(CreateTestDocument());
        var outputFile = Path.Combine(Path.GetTempPath(), $"form_{Guid.NewGuid():N}.pdf");
        try
        {
            (await _command.ExecuteAsync([inputFile, "--format=pdf", "--fillable", $"--output={outputFile}"])).Should().Be(0);
            var bytes = await File.ReadAllBytesAsync(outputFile);
            System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
            System.Text.Encoding.Latin1.GetString(bytes).Should().Contain("/AcroForm");
        }
        finally
        {
            File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }
}
