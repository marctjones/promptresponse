using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.ViewModels.Workflows;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels.Workflows;

public sealed class ExpressionWorkflowTests
{
    [Fact]
    public void Apply_RecomputesValuesBeforeApplyingDependentPromptState()
    {
        var document = new AprDocument
        {
            Sections =
            [
                new Section
                {
                    Id = "details",
                    Title = "Details",
                    Prompts =
                    [
                        new Prompt { Id = "quantity", Label = "Quantity", Response = "2", Hints = new PromptHints { ExpectedDataType = "number" } },
                        new Prompt { Id = "price", Label = "Price", Response = "3", Hints = new PromptHints { ExpectedDataType = "number" } },
                        new Prompt { Id = "total", Label = "Total", Hints = new PromptHints { ExpectedDataType = "number", ExprValue = "quantity * price" } },
                        new Prompt { Id = "notice", Label = "Notice", Hints = new PromptHints { ExprHidden = "total == 6" } },
                    ],
                },
            ],
        };
        var factory = new PromptViewModelFactory(new ProfileService(new FixedAccessibilityProbe(), applyAffordanceDefaults: false));
        var viewModels = document.Sections.Single().Prompts.Select(factory.Create).ToArray();
        var workflow = new ExpressionWorkflow(() => viewModels);

        workflow.Apply(document);

        viewModels.Single(viewModel => viewModel.Id == "total").Response.Should().Be("6");
        viewModels.Single(viewModel => viewModel.Id == "notice").IsVisible.Should().BeFalse(
            "visibility expressions must observe recomputed values");
        workflow.IsApplying.Should().BeFalse("the guard is always released after an evaluation");
    }

    private sealed class FixedAccessibilityProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }
}
