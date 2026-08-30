using AwesomeAssertions;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands;

public partial class ExportCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WithNoArgs_ShouldReturnError()
    {
        (await _command.ExecuteAsync(Array.Empty<string>())).Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentFile_ShouldReturnError()
    {
        (await _command.ExecuteAsync(["nonexistent.apr"])).Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnsupportedFormat_ShouldReturnError()
    {
        var inputFile = await WriteDocumentAsync(CreateTestDocument());
        try
        {
            (await _command.ExecuteAsync([inputFile, "--format=xml"])).Should().Be(1);
        }
        finally
        {
            File.Delete(inputFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_PdfFormat_WithoutOutput_ShouldReturnError()
    {
        var inputFile = await WriteDocumentAsync(CreateTestDocument());
        try
        {
            // PDF is binary and must go to a file, not stdout.
            (await _command.ExecuteAsync([inputFile, "--format=pdf"])).Should().Be(1);
        }
        finally
        {
            File.Delete(inputFile);
        }
    }
}
