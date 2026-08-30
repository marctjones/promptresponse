using AwesomeAssertions;
using PromptResponse.Cli.Commands.Diff;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands.Diff;

public class DocumentDiffComparerTests
{
    [Fact]
    public void Compare_ResponseAndLabelChanges_ReportsEachDifferenceInStableOrder()
    {
        var first = CreateDocument("Question", "first");
        var second = CreateDocument("Renamed", "second");

        var differences = DocumentDiffComparer.Compare(first, second);

        differences.Should().BeEquivalentTo(
            new[]
            {
                new Difference("Response", "Section 'Answers' / 'Question'", "first", "second"),
                new Difference("Label", "Section 'Answers' / Prompt 'answer'", "Question", "Renamed"),
            },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Compare_MissingNestedPrompt_DescribesThePresentPrompt()
    {
        var first = CreateDocument("Question", "first");
        var second = CreateDocument("Question", "first");
        second.Sections[0].Sections.Add(new Section
        {
            Id = "nested",
            Title = "Details",
            Prompts = new List<Prompt> { new() { Id = "extra", Label = "Extra", Response = "value" } },
        });

        var differences = DocumentDiffComparer.Compare(first, second);

        differences.Should().ContainSingle().Which.Should().Be(
            new Difference("Structure", "Section 'Answers' / Section[0]", null, "Details"));
    }

    private static AprDocument CreateDocument(string label, string response) => new()
    {
        Version = AprFormat.CurrentVersion,
        DocumentType = DocumentType.Template,
        Metadata = new Metadata { Title = "Comparison" },
        Sections = new List<Section>
        {
            new()
            {
                Id = "answers",
                Title = "Answers",
                Prompts = new List<Prompt> { new() { Id = "answer", Label = label, Response = response } },
            },
        },
    };
}
