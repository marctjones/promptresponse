using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Review;
using Xunit;

namespace PromptResponse.Core.Tests.Review;

/// <summary>
/// The receiving end's report: can this submission be processed automatically?
/// </summary>
/// <remarks>
/// A different question from validity, and the one a receiver actually has. Because the
/// format never rejects a response, every submission that parses is valid, so validity
/// tells the receiver nothing about whether their pipeline can read it.
/// </remarks>
public class FormReviewerTests
{
    private static AprDocument FormWith(params Prompt[] prompts) => new()
    {
        DocumentType = DocumentType.FilledForm,
        Metadata = new Metadata { Title = "Claim", TemplateId = "c", TemplateVersion = "1.0" },
        Sections = [new Section { Id = "s", Title = "Claim", Prompts = [.. prompts] }],
    };

    private static Prompt Field(string id, string response, PromptHints? hints = null) =>
        new() { Id = id, Label = id, Response = response, Hints = hints ?? new PromptHints() };

    // ── The headline signal: the author's own rule ──

    [Fact]
    public void AFailedValidationExpression_IsTheStrongestFinding()
    {
        var review = FormReviewer.Review(FormWith(
            Field("subtotal", "100.00", new PromptHints { ExpectedDataType = "currency" }),
            Field("tax", "20.00", new PromptHints { ExpectedDataType = "currency" }),
            Field("total", "150.00", new PromptHints
            {
                ExpectedDataType = "currency",
                ExprValidation = "double(subtotal) + double(tax) == double(total) ? '' : 'Total does not add up'",
            })));

        review.Findings.Should().ContainSingle(f => f.Code == "RULE_FAILED")
            .Which.Severity.Should().Be(ReviewSeverity.NeedsReview,
                "the form's author wrote this rule themselves, which is far stronger evidence " +
                "than any guess the tooling could make about what an answer should look like");
        review.Verdict.Should().Be(ReviewVerdict.ReviewRequired);
    }

    [Fact]
    public void ASatisfiedValidationExpression_SaysNothing()
    {
        var review = FormReviewer.Review(FormWith(
            Field("subtotal", "100.00", new PromptHints { ExpectedDataType = "currency" }),
            Field("tax", "20.00", new PromptHints { ExpectedDataType = "currency" }),
            Field("total", "120.00", new PromptHints
            {
                ExpectedDataType = "currency",
                ExprValidation = "double(subtotal) + double(tax) == double(total) ? '' : 'Total does not add up'",
            })));

        review.Verdict.Should().Be(ReviewVerdict.Processable);
        review.Findings.Should().BeEmpty();
    }

    // ── What gets looked at at all ──

    /// <summary>Regression: an ordinary prompt has no exprExpected, and must still be reviewed.</summary>
    /// <remarks>
    /// The first draft skipped any prompt where IsExpected returned false, and IsExpected
    /// returns false when there is no exprExpected at all - which is almost every prompt
    /// ever written. The reviewer skipped the entire document and reported a clean bill of
    /// health for a form it had not looked at. A tool that says "all clear" without
    /// looking is worse than no tool.
    /// </remarks>
    [Fact]
    public void OrdinaryPromptsWithNoExpressions_AreReviewed()
    {
        var review = FormReviewer.Review(FormWith(
            Field("a", "ada@example.com", new PromptHints { ExpectedDataType = "email" }),
            Field("b", "nonsense", new PromptHints { ExpectedDataType = "email" })));

        review.PromptsConsidered.Should().Be(2,
            "a prompt without exprExpected is simply unmarked, not unwanted");
        review.Findings.Should().ContainSingle(f => f.Code == "TYPE_MISMATCH" && f.PromptId == "b");
    }

    [Fact]
    public void HiddenFields_AreSkippedEntirely()
    {
        var review = FormReviewer.Review(FormWith(
            Field("visible", "yes", new PromptHints { ExpectedDataType = "text" }),
            Field("hidden", "", new PromptHints { ExprHidden = "true" })));

        review.PromptsConsidered.Should().Be(1,
            "the form says this question does not apply, so an empty answer is not a gap");
        review.Findings.Should().NotContain(f => f.PromptId == "hidden");
    }

    [Fact]
    public void AConditionalFieldTheFormIsNotAskingFor_IsNotFlaggedForBeingBlank()
    {
        // receipt_ref is only expected over 500; this claim is 150.
        var review = FormReviewer.Review(FormWith(
            Field("total", "150.00", new PromptHints { ExpectedDataType = "currency" }),
            Field("receipt_ref", "", new PromptHints { ExprExpected = "double(total) > 500.0" })));

        review.Findings.Should().NotContain(f => f.PromptId == "receipt_ref",
            "flagging every branch that does not apply would bury the real findings under " +
            "noise from questions the filler was right to skip");
        review.BlankCount.Should().Be(0);
    }

    // ── Severity is about processability, never about correctness ──

    [Theory]
    [InlineData("OUTSIDE_SUGGESTED", ReviewSeverity.Advisory)]
    [InlineData("OUTSIDE_BOUNDS", ReviewSeverity.Advisory)]
    [InlineData("BLANK", ReviewSeverity.Advisory)]
    [InlineData("TYPE_MISMATCH", ReviewSeverity.NeedsReview)]
    public void FindingsAreGradedByWhetherAMachineCanReadThem(string code, ReviewSeverity expected)
    {
        var review = FormReviewer.Review(FormWith(
            Field("suggested", "Skunkworks", new PromptHints
            {
                ExpectedDataType = "select",
                SuggestedValues = ["Sales", "Finance"],
            }),
            Field("bounded", "11", new PromptHints
            {
                ExpectedDataType = "range", Min = "1", Max = "10",
            }),
            Field("blank", "", new PromptHints { ExpectedDataType = "text" }),
            Field("mistyped", "nonsense", new PromptHints { ExpectedDataType = "email" })));

        review.Findings.Where(f => f.Code == code).Should().NotBeEmpty($"{code} should be reported")
            .And.OnlyContain(f => f.Severity == expected);
    }

    [Fact]
    public void AnUnusualAnswerAloneOnlyRecommendsReview()
    {
        var review = FormReviewer.Review(FormWith(
            Field("dept", "Skunkworks", new PromptHints
            {
                ExpectedDataType = "select",
                SuggestedValues = ["Sales", "Finance"],
            })));

        review.Verdict.Should().Be(ReviewVerdict.ReviewRecommended,
            "answering outside the suggested options is explicitly allowed by the format " +
            "and is often exactly right; it should not stop a pipeline on its own");
    }

    /// <summary>Nothing this reports ever means the document is invalid.</summary>
    [Fact]
    public void EvenTheWorstReview_SaysTheContentIsValid()
    {
        var review = FormReviewer.Review(FormWith(
            Field("email", "definitely not an email", new PromptHints { ExpectedDataType = "email" }),
            Field("amount", "a fiver", new PromptHints { ExpectedDataType = "currency" })));

        review.Verdict.Should().Be(ReviewVerdict.ReviewRequired);
        review.ContentIsAlwaysValid.Should().BeTrue(
            "a response can never be invalid (specification 3.3, 6.1). This report is about " +
            "whether a machine can read it, which is the receiver's question, not the format's");
    }

    [Fact]
    public void FindingsCarryEnoughToActOnWithoutOpeningTheFile()
    {
        var review = FormReviewer.Review(new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata { Title = "Nested", TemplateId = "n", TemplateVersion = "1.0" },
            Sections =
            [
                new Section
                {
                    Id = "outer", Title = "Employment",
                    Sections =
                    [
                        new Section
                        {
                            Id = "inner", Title = "Previous employer",
                            Prompts = [Field("email", "nope", new PromptHints { ExpectedDataType = "email" })],
                        },
                    ],
                },
            ],
        });

        var finding = review.Findings.Should().ContainSingle().Subject;
        finding.PromptId.Should().Be("email", "so a program can address the field");
        finding.SectionPath.Should().Be("Employment / Previous employer", "so a person can find it");
        finding.Response.Should().Be("nope", "so a reviewer need not open the file");
    }
}
