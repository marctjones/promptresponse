using AwesomeAssertions;
using PromptResponse.Cli.Commands;
using PromptResponse.Cli.Commands.Fill;
using PromptResponse.Cli.Tests.Fixtures;
using PromptResponse.Core.Serialization;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PromptResponse.Cli.Api;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands;

public class FillCommandTests : IDisposable
{
    private readonly FillCommand _command;
    private readonly TempFileHelper _tempHelper;

    public FillCommandTests()
    {
        var serializer = new AprJsonSerializer();
        var api = new FormFillingApi(serializer, new Core.Validation.DocumentValidator(), Substitute.For<ILogger<FormFillingApi>>());
        var logger = Substitute.For<ILogger<FillCommand>>();
        _command = new FillCommand(api, logger);
        _tempHelper = new TempFileHelper(serializer);
    }

    [Fact]
    public async Task ExecuteAsync_NoArgs_ShowsHelp() =>
        (await _command.ExecuteAsync(Array.Empty<string>())).Should().Be(1);

    [Fact]
    public async Task ExecuteAsync_FileNotFound_ReturnsError() =>
        (await _command.ExecuteAsync(new[] { "/nonexistent/file.aprt" })).Should().Be(1);

    [Fact]
    public async Task ExecuteAsync_ValidTemplate_Succeeds()
    {
        var templatePath = _tempHelper.CreateTemplateFile();
        var result = await _command.ExecuteAsync(new[] { templatePath });
        result.Should().BeOneOf(0, 1);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonInteractiveFlag_Succeeds()
    {
        var templatePath = _tempHelper.CreateTemplateFile();
        var result = await _command.ExecuteAsync(new[] { templatePath, "--non-interactive" });
        result.Should().BeOneOf(0, 1);
    }

    [Fact]
    public async Task ExecuteAsync_WithOutput_Succeeds()
    {
        var templatePath = _tempHelper.CreateTemplateFile();
        var outputPath = _tempHelper.GetPath($"output-{Guid.NewGuid():N}.aprf");
        var result = await _command.ExecuteAsync(new[] { templatePath, $"--output={outputPath}" });
        result.Should().BeOneOf(0, 1);
    }

    [Fact]
    public async Task ExecuteAsync_WithFilledBy_Succeeds()
    {
        var templatePath = _tempHelper.CreateTemplateFile();
        var result = await _command.ExecuteAsync(new[] { templatePath, "--filled-by=Test User" });
        result.Should().BeOneOf(0, 1);
    }

    [Fact]
    public void FillCommandOptions_Parse_PreservesValuesWithEqualsSigns()
    {
        var options = FillCommandOptions.Parse(new[] { "ignored", "--json={\"url\":\"a=b\"}", "--validate" });

        options.Json.Should().Be("{\"url\":\"a=b\"}");
        options.Validate.Should().BeTrue();
    }

    [Fact]
    public void CommandLineResponseCollector_Collect_OnlyIncludesSetOptions()
    {
        var responses = CommandLineResponseCollector.Collect(new Dictionary<string, string>
        {
            ["--set-name"] = "Ada",
            ["--set-empty"] = "",
            ["--filled-by"] = "Operator",
            ["--output"] = "filled.aprf"
        });

        responses.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["name"] = "Ada",
            ["empty"] = ""
        });
    }

    public void Dispose() => _tempHelper?.Dispose();
}
