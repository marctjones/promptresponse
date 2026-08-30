using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.ViewModels.Workflows;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels.Workflows;

public sealed class AdvisoryWorkflowTests
{
    [Fact]
    public void Refresh_MapsNestedValidatorWarningsToThePromptLabel_AndSignalsStateChanged()
    {
        var workflow = new AdvisoryWorkflow();
        var notifications = 0;
        workflow.StateChanged += () => notifications++;
        var document = new AprDocument
        {
            Sections =
            [
                new Section
                {
                    Id = "outer",
                    Title = "Outer",
                    Sections =
                    [
                        new Section
                        {
                            Id = "inner",
                            Title = "Inner",
                            Prompts =
                            [new Prompt
                            {
                                Id = "amount",
                                Label = "Amount due",
                                Response = "five",
                                Hints = new PromptHints { ExpectedDataType = "number" },
                            }],
                        },
                    ],
                },
            ],
        };

        workflow.Refresh(document);

        workflow.Items.Should().ContainSingle(item => item.PromptId == "amount"
            && item.PromptLabel == "Amount due");
        notifications.Should().Be(1);
    }

    [Fact]
    public void Refresh_NullDocument_ClearsExistingItems_AndSignalsStateChanged()
    {
        var workflow = new AdvisoryWorkflow();
        var notifications = 0;
        workflow.StateChanged += () => notifications++;
        workflow.Refresh(DocumentWithInvalidNumber());

        workflow.Refresh(null);

        workflow.Items.Should().BeEmpty();
        notifications.Should().Be(2);
    }

    private static AprDocument DocumentWithInvalidNumber() => new()
    {
        Sections =
        [
            new Section
            {
                Id = "section",
                Title = "Section",
                Prompts =
                [new Prompt
                {
                    Id = "amount",
                    Label = "Amount",
                    Response = "five",
                    Hints = new PromptHints { ExpectedDataType = "number" },
                }],
            },
        ],
    };
}
