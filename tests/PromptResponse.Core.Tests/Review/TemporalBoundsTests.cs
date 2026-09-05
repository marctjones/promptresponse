using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Review;
using Xunit;

namespace PromptResponse.Core.Tests.Review;

/// <summary>
/// Bounds on an ordered field are an offer, not a limit.
/// </summary>
/// <remarks>
/// The specification is explicit that a response outside <c>min</c> or <c>max</c> is
/// still valid and a validator MUST NOT reject one: a slider that stops at 100 does
/// not make "120" a wrong answer. The temporal half of that rule had no test at all,
/// so nothing stopped a reader from turning a date bound into a limit.
/// </remarks>
public sealed class TemporalBoundsTests
{
    private static AprDocument Document(string type, string response, string? min, string? max) => new()
    {
        Metadata = new Metadata { Title = "T" },
        Sections =
        [
            new Section
            {
                Id = "s",
                Title = "S",
                Prompts =
                [
                    new Prompt
                    {
                        Id = "when",
                        Label = "When",
                        Response = response,
                        Hints = new PromptHints { ExpectedDataType = type, Min = min, Max = max },
                    },
                ],
            },
        ],
    };

    [Theory]
    [InlineData("date", "2020-01-01", "2024-01-01", "2026-12-31")]
    [InlineData("date", "2030-06-01", "2024-01-01", "2026-12-31")]
    [InlineData("datetime", "2020-01-01T09:00:00Z", "2024-01-01T00:00:00Z", null)]
    [InlineData("time", "06:00", "09:00", "17:00")]
    public void AResponseOutsideItsTemporalBounds_IsAdvisedAndStillValid(
        string type, string response, string? min, string? max)
    {
        var document = Document(type, response, min, max);

        var review = FormReviewer.Review(document);

        review.Findings.Should().NotBeEmpty(
            "a response outside a suggested bound is worth telling someone about");
        // ReviewSeverity has no Error member on purpose: neither level means the
        // document is invalid, so there is nothing for an Error to mean. A response
        // outside a suggested bound is Advisory - unusual but plausible - and never
        // NeedsReview, which is reserved for a value a machine will mishandle.
        review.Findings.Should().OnlyContain(
            finding => finding.Severity != ReviewSeverity.NeedsReview,
            "bounds are an offer, not a limit: a response outside them is still valid");
    }

    [Theory]
    [InlineData("date", "2025-06-01", "2024-01-01", "2026-12-31")]
    [InlineData("time", "12:00", "09:00", "17:00")]
    public void AResponseInsideItsTemporalBounds_IsNotAdvised(
        string type, string response, string min, string max)
    {
        var review = FormReviewer.Review(Document(type, response, min, max));

        review.Findings.Should().BeEmpty("the response sits inside the range the author offered");
    }

    [Fact]
    public void AnUnparseableTemporalResponse_IsNeverComparedAgainstBounds()
    {
        // Any string is a valid response. "next Tuesday" cannot be ordered against a
        // date, so the bound simply does not apply rather than producing a bound
        // advisory about a value it could not read.
        var review = FormReviewer.Review(Document("date", "next Tuesday", "2024-01-01", "2026-12-31"));

        review.Findings.Should().NotContain(finding => finding.Code.Contains("BOUND"),
            "an unorderable response is outside no range");
    }

    [Fact]
    public void AbsentBounds_ProduceNoTemporalAdvisory()
    {
        var review = FormReviewer.Review(Document("date", "1066-10-14", null, null));

        review.Findings.Should().BeEmpty("with no bounds offered there is nothing to be outside of");
    }
}
