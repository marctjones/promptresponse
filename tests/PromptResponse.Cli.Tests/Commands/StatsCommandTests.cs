using FluentAssertions;
using PromptResponse.Cli.Commands;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands;

/// <summary>
/// Unit tests for StatsCommand.
/// </summary>
public class StatsCommandTests
{
    private readonly AprJsonSerializer _serializer;
    private readonly StatsCommand _command;

    public StatsCommandTests()
    {
        _serializer = new AprJsonSerializer();
        _command = new StatsCommand(_serializer);
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
        var args = new[] { "nonexistent-file.apr" };

        // Act
        var exitCode = await _command.ExecuteAsync(args);

        // Assert
        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidFile_ShouldReturnSuccess()
    {
        // Arrange
        var document = CreateTestDocument();
        var tempFile = Path.GetTempFileName();

        try
        {
            // Write test document
            var json = _serializer.Serialize(document);
            await File.WriteAllTextAsync(tempFile, json);

            var args = new[] { tempFile };

            // Act
            var exitCode = await _command.ExecuteAsync(args);

            // Assert
            exitCode.Should().Be(0);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithJsonFlag_ShouldReturnSuccess()
    {
        // Arrange
        var document = CreateTestDocument();
        var tempFile = Path.GetTempFileName();

        try
        {
            // Write test document
            var json = _serializer.Serialize(document);
            await File.WriteAllTextAsync(tempFile, json);

            var args = new[] { tempFile, "--json" };

            // Act
            var exitCode = await _command.ExecuteAsync(args);

            // Assert
            exitCode.Should().Be(0);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithGovernmentForm_ShouldCalculateStatistics()
    {
        // Arrange
        var examplePath = GetExampleFilePath("irs-form-w4-2024.aprt");
        if (File.Exists(examplePath))
        {
            var args = new[] { examplePath };

            // Act
            var exitCode = await _command.ExecuteAsync(args);

            // Assert
            exitCode.Should().Be(0);
        }
    }

    private AprDocument CreateTestDocument()
    {
        return new AprDocument
        {
            Version = "1.0",
            DocumentType = DocumentType.Template,
            Metadata = new Metadata
            {
                Title = "Test Form",
                Author = "Test Author",
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
                                ExpectedDataType = "text",
                                HelpText = "Enter your answer"
                            }
                        },
                        new()
                        {
                            Id = "prompt2",
                            Label = "Number Question",
                            Response = "",
                            Hints = new PromptHints
                            {
                                ExpectedDataType = "number"
                            }
                        }
                    }
                }
            }
        };
    }

    private static string GetExampleFilePath(string filename)
    {
        var testDir = Directory.GetCurrentDirectory();
        var projectRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        var examplesDir = Path.Combine(projectRoot, "examples");
        return Path.Combine(examplesDir, filename);
    }
}
