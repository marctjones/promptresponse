using FluentAssertions;
using PromptResponse.Cli.Commands;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands;

/// <summary>
/// Unit tests for DiffCommand.
/// </summary>
public class DiffCommandTests
{
    private readonly AprJsonSerializer _serializer;
    private readonly DiffCommand _command;

    public DiffCommandTests()
    {
        _serializer = new AprJsonSerializer();
        _command = new DiffCommand(_serializer);
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
    public async Task ExecuteAsync_WithOneArg_ShouldReturnError()
    {
        // Arrange
        var args = new[] { "file1.apr" };

        // Act
        var exitCode = await _command.ExecuteAsync(args);

        // Assert
        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentFile1_ShouldReturnError()
    {
        // Arrange
        var args = new[] { "nonexistent1.apr", "nonexistent2.apr" };

        // Act
        var exitCode = await _command.ExecuteAsync(args);

        // Assert
        exitCode.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithIdenticalFiles_ShouldReturnSuccess()
    {
        // Arrange
        var document = CreateTestDocument();
        var tempFile1 = Path.GetTempFileName();
        var tempFile2 = Path.GetTempFileName();

        try
        {
            // Write same document to both files
            var json = _serializer.Serialize(document);
            await File.WriteAllTextAsync(tempFile1, json);
            await File.WriteAllTextAsync(tempFile2, json);

            var args = new[] { tempFile1, tempFile2 };

            // Act
            var exitCode = await _command.ExecuteAsync(args);

            // Assert
            exitCode.Should().Be(0); // Identical files return 0
        }
        finally
        {
            if (File.Exists(tempFile1)) File.Delete(tempFile1);
            if (File.Exists(tempFile2)) File.Delete(tempFile2);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithDifferentResponses_ShouldDetectDifferences()
    {
        // Arrange
        var document1 = CreateTestDocument();
        document1.Sections[0].Prompts[0].Response = "Answer 1";

        var document2 = CreateTestDocument();
        document2.Sections[0].Prompts[0].Response = "Answer 2";

        var tempFile1 = Path.GetTempFileName();
        var tempFile2 = Path.GetTempFileName();

        try
        {
            // Write different documents
            var json1 = _serializer.Serialize(document1);
            var json2 = _serializer.Serialize(document2);
            await File.WriteAllTextAsync(tempFile1, json1);
            await File.WriteAllTextAsync(tempFile2, json2);

            var args = new[] { tempFile1, tempFile2 };

            // Act
            var exitCode = await _command.ExecuteAsync(args);

            // Assert
            exitCode.Should().Be(1); // Different files return 1
        }
        finally
        {
            if (File.Exists(tempFile1)) File.Delete(tempFile1);
            if (File.Exists(tempFile2)) File.Delete(tempFile2);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithDifferentTitles_ShouldDetectDifferences()
    {
        // Arrange
        var document1 = CreateTestDocument();
        document1.Metadata.Title = "Title 1";

        var document2 = CreateTestDocument();
        document2.Metadata.Title = "Title 2";

        var tempFile1 = Path.GetTempFileName();
        var tempFile2 = Path.GetTempFileName();

        try
        {
            // Write different documents
            var json1 = _serializer.Serialize(document1);
            var json2 = _serializer.Serialize(document2);
            await File.WriteAllTextAsync(tempFile1, json1);
            await File.WriteAllTextAsync(tempFile2, json2);

            var args = new[] { tempFile1, tempFile2 };

            // Act
            var exitCode = await _command.ExecuteAsync(args);

            // Assert
            exitCode.Should().Be(1);
        }
        finally
        {
            if (File.Exists(tempFile1)) File.Delete(tempFile1);
            if (File.Exists(tempFile2)) File.Delete(tempFile2);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithDifferentSectionCount_ShouldDetectDifferences()
    {
        // Arrange
        var document1 = CreateTestDocument();

        var document2 = CreateTestDocument();
        document2.Sections.Add(new Section
        {
            Id = "section2",
            Title = "Additional Section",
            Prompts = new List<Prompt>
            {
                new() { Id = "prompt3", Label = "Question 3", Response = "" }
            }
        });

        var tempFile1 = Path.GetTempFileName();
        var tempFile2 = Path.GetTempFileName();

        try
        {
            // Write different documents
            var json1 = _serializer.Serialize(document1);
            var json2 = _serializer.Serialize(document2);
            await File.WriteAllTextAsync(tempFile1, json1);
            await File.WriteAllTextAsync(tempFile2, json2);

            var args = new[] { tempFile1, tempFile2 };

            // Act
            var exitCode = await _command.ExecuteAsync(args);

            // Assert
            exitCode.Should().Be(1);
        }
        finally
        {
            if (File.Exists(tempFile1)) File.Delete(tempFile1);
            if (File.Exists(tempFile2)) File.Delete(tempFile2);
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
                            Response = ""
                        }
                    }
                }
            }
        };
    }
}
