using AwesomeAssertions;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands;

public partial class DiffCommandTests
{
    [Fact]
    public async Task ExecuteAsync_BothFilesMissing_ReturnsError()
    {
        var exit = await _command.ExecuteAsync(new[] { "missing1.apr", "missing2.apr" });

        exit.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_FirstFileMalformedJson_ReturnsError()
    {
        var bad = await CreateMalformedFileAsync();
        var ok = _tempHelper.CreateTempFile(CreateTestDocument());

        try
        {
            var exit = await _command.ExecuteAsync(new[] { bad, ok });

            exit.Should().Be(1);
        }
        finally
        {
            File.Delete(bad);
        }
    }

    [Fact]
    public async Task ExecuteAsync_SecondFileMalformedJson_ReturnsError()
    {
        var ok = _tempHelper.CreateTempFile(CreateTestDocument());
        var bad = await CreateMalformedFileAsync();

        try
        {
            var exit = await _command.ExecuteAsync(new[] { ok, bad });

            exit.Should().Be(1);
        }
        finally
        {
            File.Delete(bad);
        }
    }

    private static async Task<string> CreateMalformedFileAsync()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "not json {");
        return path;
    }
}
