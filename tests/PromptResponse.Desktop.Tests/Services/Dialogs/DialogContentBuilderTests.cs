using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Rendering;
using PromptResponse.Desktop.Services.Dialogs;
using PromptResponse.Rendering.Pdf;

namespace PromptResponse.Desktop.Tests.Services.Dialogs;

public class DialogContentBuilderTests
{
    [AvaloniaFact]
    public void PrintPreview_UsesTheSemanticBlocksAndEmptyFieldSetting()
    {
        var model = new RenderModel(
            "Expense report",
            "June travel",
            DocumentType.FilledForm,
            new RenderBlock[]
            {
                new HeadingBlock(1, "Trip", "Business travel"),
                new FieldBlock("Destination", "", false, "Where the travel occurred", null),
                new TableBlock(new[] { "Date", "Amount" }, new[]
                {
                    new TableRowBlock("Flight", new[] { new TableCellBlock("", false), new TableCellBlock("120", true) }),
                }),
            });

        var content = PrintPreviewContentBuilder.Build(model, includeEmptyFields: true).Should().BeOfType<StackPanel>().Subject;
        var text = content.Children.OfType<TextBlock>().Select(block => block.Text).ToArray();

        text.Should().Contain("Expense report");
        text.Should().Contain("June travel");
        text.Should().Contain("PDF export preview - Letter page size - blank fields included");
        text.Should().Contain("Trip");
        text.Should().Contain("Destination");
        content.Children.OfType<StackPanel>().Single().Children.OfType<TextBlock>().Select(block => block.Text)
            .Should().Contain(new[] { "Date | Amount", "Flight: [blank] | 120" });
    }

    [AvaloniaFact]
    public void ImportReview_ShowsAFlagSummaryAndCapsSamplesAtTwelve()
    {
        var flags = Enumerable.Range(1, 13)
            .Select(index => new FieldFlag($"field-{index}", $"Field {index}", FieldFlagKind.CrypticLabel, "Needs a human label"))
            .ToArray();
        var quality = new ImportQuality(42, "F", ImportRecommendation.UseSkillInstead, "Labels are unreliable.", 13, 0.1, 0.9, 0.2, flags);

        var content = ImportReviewContentBuilder.Build(quality).Should().BeOfType<StackPanel>().Subject;
        var text = content.Children.OfType<TextBlock>().Select(block => block.Text).ToArray();

        text.Should().Contain("PDF Import Needs Review");
        text.Should().Contain("Flag summary: Cryptic label: 13");
        text.Should().Contain("Cryptic label - Field 12 (field-12): Needs a human label");
        text.Should().NotContain("Cryptic label - Field 13 (field-13): Needs a human label");
        text.Should().Contain("...and 1 more flagged fields.");
    }
}
