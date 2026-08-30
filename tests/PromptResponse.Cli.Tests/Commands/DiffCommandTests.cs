using AwesomeAssertions;
using PromptResponse.Cli.Commands;
using PromptResponse.Cli.Tests.Fixtures;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands;

/// <summary>
/// Unit tests for DiffCommand.
/// </summary>
public partial class DiffCommandTests : IDisposable
{
    private readonly DiffCommand _command;
    private readonly TempFileHelper _tempHelper;

    public DiffCommandTests()
    {
        var serializer = new AprJsonSerializer();
        _command = new DiffCommand(serializer);
        _tempHelper = new TempFileHelper(serializer);
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
        var tempFile1 = _tempHelper.CreateTempFile(document);
        var tempFile2 = _tempHelper.CreateTempFile(document);

        // Act
        var exitCode = await _command.ExecuteAsync(new[] { tempFile1, tempFile2 });

        // Assert
        exitCode.Should().Be(0); // Identical files return 0
    }

    [Fact]
    public async Task ExecuteAsync_WithDifferentResponses_ShouldDetectDifferences()
    {
        // Arrange
        var document1 = CreateTestDocument();
        document1.Sections[0].Prompts[0].Response = "Answer 1";

        var document2 = CreateTestDocument();
        document2.Sections[0].Prompts[0].Response = "Answer 2";

        var tempFile1 = _tempHelper.CreateTempFile(document1);
        var tempFile2 = _tempHelper.CreateTempFile(document2);

        // Act
        var exitCode = await _command.ExecuteAsync(new[] { tempFile1, tempFile2 });

        // Assert
        exitCode.Should().Be(1); // Different files return 1
    }

    [Fact]
    public async Task ExecuteAsync_WithDifferentTitles_ShouldDetectDifferences()
    {
        // Arrange
        var document1 = CreateTestDocument();
        document1.Metadata.Title = "Title 1";

        var document2 = CreateTestDocument();
        document2.Metadata.Title = "Title 2";

        var tempFile1 = _tempHelper.CreateTempFile(document1);
        var tempFile2 = _tempHelper.CreateTempFile(document2);

        // Act
        var exitCode = await _command.ExecuteAsync(new[] { tempFile1, tempFile2 });

        // Assert
        exitCode.Should().Be(1);
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

        var tempFile1 = _tempHelper.CreateTempFile(document1);
        var tempFile2 = _tempHelper.CreateTempFile(document2);

        // Act
        var exitCode = await _command.ExecuteAsync(new[] { tempFile1, tempFile2 });

        // Assert
        exitCode.Should().Be(1);
    }

    private AprDocument CreateTestDocument()
    {
        return new AprDocument
        {
            Version = AprFormat.CurrentVersion,
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

    public void Dispose() => _tempHelper?.Dispose();
}
