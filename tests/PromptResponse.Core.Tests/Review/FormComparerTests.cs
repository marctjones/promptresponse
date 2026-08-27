using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Review;
using Xunit;

namespace PromptResponse.Core.Tests.Review;

/// <summary>
/// Is this the form we published, or a form that looks like it?
/// </summary>
/// <remarks>
/// A submission can be valid, parse cleanly, pass every processability check, and still
/// be answering different questions. These pin the cases a receiver needs told about.
/// </remarks>
public class FormComparerTests
{
    private static AprDocument Form(DocumentType type, params Prompt[] prompts) => new()
    {
        DocumentType = type,
        Metadata = new Metadata { Title = "Questionnaire", TemplateId = "q", TemplateVersion = "2.0" },
        Sections = [new Section { Id = "s", Title = "Background", Prompts = [.. prompts] }],
    };

    private static Prompt Ask(string id, string label, string? type = "text", string response = "") =>
        new()
        {
            Id = id, Label = label, Response = response,
            Hints = new PromptHints { ExpectedDataType = type },
        };

    private static AprDocument Template(params Prompt[] p) => Form(DocumentType.Template, p);
    private static AprDocument Submission(params Prompt[] p) => Form(DocumentType.FilledForm, p);

    [Fact]
    public void AFaithfulSubmission_IsIdentical()
    {
        var comparison = FormComparer.Compare(
            Template(Ask("q1", "Current employer")),
            Submission(Ask("q1", "Current employer", response: "Acme Ltd")));

        comparison.DefinitionIdentical.Should().BeTrue(
            "answering a form must not change its definition; the comparison uses the same " +
            "canonical bytes a publisher signature binds, which exclude responses");
        comparison.Findings.Should().BeEmpty();
    }

    /// <summary>The case that motivates the whole comparison.</summary>
    /// <remarks>
    /// The id is unchanged, so a pipeline mapping responses by id files this answer under
    /// the question it thinks was asked. The response itself looks perfect - "No" is a
    /// fine boolean - so nothing in a processability review will ever catch it. Only the
    /// template knows.
    /// </remarks>
    [Fact]
    public void AQuestionRewrittenUnderTheSameId_IsCaught()
    {
        var comparison = FormComparer.Compare(
            Template(Ask("q1", "Have you ever been convicted of a felony?", "boolean")),
            Submission(Ask("q1", "Have you been convicted in the last 12 months?", "boolean", "No")));

        comparison.DefinitionIdentical.Should().BeFalse();
        var finding = comparison.Findings.Should().ContainSingle(f => f.Code == "PROMPT_RELABELLED").Subject;
        finding.Severity.Should().Be(ReviewSeverity.NeedsReview);
        finding.Response.Should().Be("No",
            "the reviewer needs to see the answer that was given to the substituted question");
    }

    [Fact]
    public void AQuestionDroppedFromTheSubmission_IsCaught()
    {
        var comparison = FormComparer.Compare(
            Template(Ask("q1", "Employer"), Ask("q2", "Foreign contacts")),
            Submission(Ask("q1", "Employer", response: "Acme")));

        comparison.Findings.Should().ContainSingle(f => f.Code == "PROMPT_MISSING")
            .Which.PromptId.Should().Be("q2");
    }

    [Fact]
    public void AQuestionTheSubmitterAddedThemselves_IsCaught()
    {
        var comparison = FormComparer.Compare(
            Template(Ask("q1", "Employer")),
            Submission(Ask("q1", "Employer", response: "Acme"), Ask("q9", "Anything else", response: "no")));

        comparison.Findings.Should().ContainSingle(f => f.Code == "PROMPT_ADDED")
            .Which.PromptId.Should().Be("q9");
    }

    [Fact]
    public void AFieldWhoseTypeChanged_IsCaught()
    {
        var comparison = FormComparer.Compare(
            Template(Ask("q1", "Amount", "currency")),
            Submission(Ask("q1", "Amount", "text", "about fifty")));

        comparison.Findings.Should().ContainSingle(f => f.Code == "PROMPT_RETYPED");
    }

    [Fact]
    public void AChangedShortlist_IsCaught()
    {
        var template = Template(new Prompt
        {
            Id = "q1", Label = "Department",
            Hints = new PromptHints { ExpectedDataType = "select", SuggestedValues = ["Sales", "Finance"] },
        });
        var submission = Submission(new Prompt
        {
            Id = "q1", Label = "Department", Response = "Skunkworks",
            Hints = new PromptHints { ExpectedDataType = "select", SuggestedValues = ["Skunkworks"] },
        });

        FormComparer.Compare(template, submission).Findings
            .Should().ContainSingle(f => f.Code == "PROMPT_OPTIONS_CHANGED",
                "an answer chosen from a list the publisher never offered is not the same " +
                "as an answer chosen from theirs");
    }

    [Fact]
    public void AMismatchedTemplateIdentity_IsReported()
    {
        var template = Template(Ask("q1", "Employer"));
        var submission = Submission(Ask("q1", "Employer", response: "Acme"));
        submission.Metadata.TemplateVersion = "1.0";

        var comparison = FormComparer.Compare(template, submission);

        comparison.IdentityMatches.Should().BeFalse();
        comparison.Findings.Should().Contain(f => f.Code == "TEMPLATE_IDENTITY_MISMATCH");
    }

    /// <summary>Declared identity proves nothing on its own.</summary>
    /// <remarks>
    /// templateId and templateVersion are written by whoever wrote the file. Someone who
    /// edited the questions can edit these too, so a matching identity beside an edited
    /// form must still be reported as edited. This exists so nobody is tempted to short
    /// circuit the structural comparison when the labels on the tin agree.
    /// </remarks>
    [Fact]
    public void AMatchingIdentity_DoesNotExcuseAnEditedForm()
    {
        var comparison = FormComparer.Compare(
            Template(Ask("q1", "Have you ever been convicted of a felony?", "boolean")),
            Submission(Ask("q1", "Do you like dogs?", "boolean", "Yes")));

        comparison.IdentityMatches.Should().BeTrue("the submitter kept the same templateId and version");
        comparison.DefinitionIdentical.Should().BeFalse("but the questions are not the same");
        comparison.Findings.Should().Contain(f => f.Code == "PROMPT_RELABELLED");
    }
}
