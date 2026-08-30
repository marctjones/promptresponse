using AwesomeAssertions;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands;

public partial class DiffCommandTests
{
    [Fact]
    public async Task ExecuteAsync_NestedSectionsDiffer_ReportsNestedDifference()
    {
        var doc1 = CreateNestedDocument("Inner-A");
        var doc2 = CreateNestedDocument("Inner-B");

        var exit = await _command.ExecuteAsync(CreateFileArguments(doc1, doc2));

        exit.Should().Be(1, "nested-section title diff is a real difference");
    }

    [Fact]
    public async Task ExecuteAsync_OneDocHasExtraNestedSection_ReportsAddedSubsection()
    {
        var doc1 = CreateOuterSectionDocument();
        var doc2 = CreateNestedDocument("Added");

        var exit = await _command.ExecuteAsync(CreateFileArguments(doc1, doc2));

        exit.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_PromptCountDiffers_ReportsDifference()
    {
        var doc1 = CreateTestDocument();
        var doc2 = CreateTestDocument();
        doc2.Sections[0].Prompts.Add(new Prompt { Id = "extra", Label = "Extra", Response = "" });

        var exit = await _command.ExecuteAsync(CreateFileArguments(doc1, doc2));

        exit.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_PromptLabelDiffers_ReportsDifference()
    {
        var doc1 = CreateTestDocument();
        var doc2 = CreateTestDocument();
        doc2.Sections[0].Prompts[0].Label = "Renamed";

        var exit = await _command.ExecuteAsync(CreateFileArguments(doc1, doc2));

        exit.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_PromptIdDiffers_ReportsDifference()
    {
        var doc1 = CreateTestDocument();
        var doc2 = CreateTestDocument();
        doc2.Sections[0].Prompts[0].Id = "renamed-id";

        var exit = await _command.ExecuteAsync(CreateFileArguments(doc1, doc2));

        exit.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_EmptySectionsBoth_NoDifferences_ReturnsZero()
    {
        var doc1 = CreateEmptyDocument();
        var doc2 = CreateEmptyDocument();

        var exit = await _command.ExecuteAsync(CreateFileArguments(doc1, doc2));

        exit.Should().Be(0);
    }

    private string[] CreateFileArguments(AprDocument first, AprDocument second) =>
        new[] { _tempHelper.CreateTempFile(first), _tempHelper.CreateTempFile(second) };

    private static AprDocument CreateEmptyDocument() => new()
    {
        Version = AprFormat.CurrentVersion,
        DocumentType = DocumentType.Template,
        Metadata = new Metadata { Title = "T" },
        Sections = new List<Section>(),
    };

    private static AprDocument CreateOuterSectionDocument() => new()
    {
        Version = AprFormat.CurrentVersion,
        DocumentType = DocumentType.Template,
        Metadata = new Metadata { Title = "T" },
        Sections = new List<Section>
        {
            new() { Id = "s1", Title = "Outer", Prompts = new List<Prompt>() },
        },
    };

    private static AprDocument CreateNestedDocument(string nestedTitle) => new()
    {
        Version = AprFormat.CurrentVersion,
        DocumentType = DocumentType.Template,
        Metadata = new Metadata { Title = "T" },
        Sections = new List<Section>
        {
            new()
            {
                Id = "s1",
                Title = "Outer",
                Sections = new List<Section>
                {
                    new() { Id = "s1a", Title = nestedTitle, Prompts = new List<Prompt>() },
                },
            },
        },
    };
}
