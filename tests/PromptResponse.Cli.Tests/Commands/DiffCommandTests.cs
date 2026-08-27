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
public class DiffCommandTests : IDisposable
{
    private readonly AprJsonSerializer _serializer;
    private readonly DiffCommand _command;
    private readonly TempFileHelper _tempHelper;

    public DiffCommandTests()
    {
        _serializer = new AprJsonSerializer();
        _command = new DiffCommand(_serializer);
        _tempHelper = new TempFileHelper(_serializer);
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

    // ── Nested-section diff coverage ──

    [Fact]
    public async Task ExecuteAsync_NestedSectionsDiffer_ReportsNestedDifference()
    {
        var doc1 = new AprDocument
        {
            Version = AprFormat.CurrentVersion, DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "T" },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "s1", Title = "Outer",
                    Sections = new List<Section>
                    {
                        new() { Id = "s1a", Title = "Inner-A", Prompts = new List<Prompt>() },
                    },
                },
            },
        };
        var doc2 = new AprDocument
        {
            Version = AprFormat.CurrentVersion, DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "T" },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "s1", Title = "Outer",
                    Sections = new List<Section>
                    {
                        new() { Id = "s1a", Title = "Inner-B", Prompts = new List<Prompt>() },
                    },
                },
            },
        };

        var f1 = _tempHelper.CreateTempFile(doc1);
        var f2 = _tempHelper.CreateTempFile(doc2);
        var exit = await _command.ExecuteAsync(new[] { f1, f2 });
        exit.Should().Be(1, "nested-section title diff is a real difference");
    }

    [Fact]
    public async Task ExecuteAsync_OneDocHasExtraNestedSection_ReportsAddedSubsection()
    {
        var doc1 = new AprDocument
        {
            Version = AprFormat.CurrentVersion, DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "T" },
            Sections = new List<Section>
            {
                new() { Id = "s1", Title = "Outer", Prompts = new List<Prompt>() },
            },
        };
        var doc2 = new AprDocument
        {
            Version = AprFormat.CurrentVersion, DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "T" },
            Sections = new List<Section>
            {
                new()
                {
                    Id = "s1", Title = "Outer",
                    Sections = new List<Section>
                    {
                        new() { Id = "s1a", Title = "Added", Prompts = new List<Prompt>() },
                    },
                },
            },
        };

        var f1 = _tempHelper.CreateTempFile(doc1);
        var f2 = _tempHelper.CreateTempFile(doc2);
        var exit = await _command.ExecuteAsync(new[] { f1, f2 });
        exit.Should().Be(1);
    }

    // ── Prompt-list shape diffs ──

    [Fact]
    public async Task ExecuteAsync_PromptCountDiffers_ReportsDifference()
    {
        var doc1 = CreateTestDocument();
        var doc2 = CreateTestDocument();
        doc2.Sections[0].Prompts.Add(new Prompt { Id = "extra", Label = "Extra", Response = "" });

        var f1 = _tempHelper.CreateTempFile(doc1);
        var f2 = _tempHelper.CreateTempFile(doc2);
        var exit = await _command.ExecuteAsync(new[] { f1, f2 });
        exit.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_PromptLabelDiffers_ReportsDifference()
    {
        var doc1 = CreateTestDocument();
        var doc2 = CreateTestDocument();
        doc2.Sections[0].Prompts[0].Label = "Renamed";

        var f1 = _tempHelper.CreateTempFile(doc1);
        var f2 = _tempHelper.CreateTempFile(doc2);
        var exit = await _command.ExecuteAsync(new[] { f1, f2 });
        exit.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_PromptIdDiffers_ReportsDifference()
    {
        var doc1 = CreateTestDocument();
        var doc2 = CreateTestDocument();
        doc2.Sections[0].Prompts[0].Id = "renamed-id";

        var f1 = _tempHelper.CreateTempFile(doc1);
        var f2 = _tempHelper.CreateTempFile(doc2);
        var exit = await _command.ExecuteAsync(new[] { f1, f2 });
        exit.Should().Be(1);
    }

    // ── Edge cases ──

    [Fact]
    public async Task ExecuteAsync_BothFilesMissing_ReturnsError()
    {
        var exit = await _command.ExecuteAsync(new[] { "missing1.apr", "missing2.apr" });
        exit.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_FirstFileMalformedJson_ReturnsError()
    {
        var bad = Path.GetTempFileName();
        await File.WriteAllTextAsync(bad, "not json {");
        var doc = CreateTestDocument();
        var ok = _tempHelper.CreateTempFile(doc);
        try
        {
            var exit = await _command.ExecuteAsync(new[] { bad, ok });
            exit.Should().Be(1);
        }
        finally
        {
            if (File.Exists(bad)) File.Delete(bad);
        }
    }

    [Fact]
    public async Task ExecuteAsync_SecondFileMalformedJson_ReturnsError()
    {
        var doc = CreateTestDocument();
        var ok = _tempHelper.CreateTempFile(doc);
        var bad = Path.GetTempFileName();
        await File.WriteAllTextAsync(bad, "not json {");
        try
        {
            var exit = await _command.ExecuteAsync(new[] { ok, bad });
            exit.Should().Be(1);
        }
        finally
        {
            if (File.Exists(bad)) File.Delete(bad);
        }
    }

    [Fact]
    public async Task ExecuteAsync_EmptySectionsBoth_NoDifferences_ReturnsZero()
    {
        var doc1 = new AprDocument
        {
            Version = AprFormat.CurrentVersion, DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "T" },
            Sections = new List<Section>(),
        };
        var doc2 = new AprDocument
        {
            Version = AprFormat.CurrentVersion, DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "T" },
            Sections = new List<Section>(),
        };
        var f1 = _tempHelper.CreateTempFile(doc1);
        var f2 = _tempHelper.CreateTempFile(doc2);
        var exit = await _command.ExecuteAsync(new[] { f1, f2 });
        exit.Should().Be(0);
    }

    public void Dispose() => _tempHelper?.Dispose();
}
