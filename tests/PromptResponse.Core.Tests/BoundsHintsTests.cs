using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using Xunit;

namespace PromptResponse.Core.Tests;

/// <summary>
/// min, max and step describe the control, never the answer.
/// </summary>
/// <remarks>
/// <para>
/// The bounds family is the newest hint vocabulary and the easiest to get wrong, because
/// every other format treats min and max as validation. Here they are an offer: a slider
/// that stops at 10 does not make "11" a wrong answer, and a validator that rejected one
/// would have stopped implementing APR (specification 4.7, 3.3, 6.1).
/// </para>
/// <para>
/// They are also strings, like every value in the format bar one derived boolean
/// (specification 3.2). A bound written as a JSON number would be a second exception to a
/// rule the specification calls absolute.
/// </para>
/// </remarks>
public class BoundsHintsTests
{
    private static readonly AprJsonSerializer Serializer = new();

    private static AprDocument WithBounds(string response) => new()
    {
        DocumentType = DocumentType.FilledForm,
        Metadata = new Metadata { Title = "Bounds", TemplateId = "b", TemplateVersion = "1.0" },
        Sections =
        [
            new Section
            {
                Id = "s", Title = "Bounded",
                Prompts =
                [
                    new Prompt
                    {
                        Id = "rating", Label = "Rating", Response = response,
                        Hints = new PromptHints
                        {
                            ExpectedDataType = "range", Min = "1", Max = "10", Step = "1",
                        },
                    },
                ],
            },
        ],
    };

    [Theory]
    [InlineData("11", "above the maximum")]
    [InlineData("0", "below the minimum")]
    [InlineData("7.5", "off the step")]
    [InlineData("as high as it goes", "not a number at all")]
    [InlineData("", "left blank")]
    public void AResponseOutsideItsBounds_IsStillValid(string response, string why)
    {
        var result = new DocumentValidator().Validate(WithBounds(response));

        result.IsValid.Should().BeTrue(
            $"a response {why} must remain valid: bounds shape the control offered to " +
            "someone who wants it, they never shrink what a person is allowed to say " +
            $"(specification 4.7). Errors: {string.Join(" | ", result.Errors.Select(e => e.Message))}");
    }

    [Fact]
    public void Bounds_AreWrittenAsStrings()
    {
        var json = Serializer.Serialize(WithBounds("5"));

        json.Should().Contain("\"min\": \"1\"")
            .And.Contain("\"max\": \"10\"")
            .And.Contain("\"step\": \"1\"",
                "every value in the format is a string bar one derived boolean " +
                "(specification 3.2); a numeric bound would be a second exception to a " +
                "rule the specification calls absolute");
    }

    [Fact]
    public void Bounds_SurviveARoundTrip()
    {
        var reloaded = Serializer.Deserialize(Serializer.Serialize(WithBounds("5")));
        var hints = reloaded.Sections[0].Prompts[0].Hints;

        hints.Min.Should().Be("1");
        hints.Max.Should().Be("10");
        hints.Step.Should().Be("1");
    }

    /// <summary>A reader that has never heard of bounds must still work.</summary>
    /// <remarks>
    /// Specification 4.7: an unrecognised hint degrades rather than erroring. Bounds were
    /// added after 1.0-beta.6 shipped, so a reader older than they are must load this
    /// document and offer a plain field.
    /// </remarks>
    [Fact]
    public void ADocumentWithBounds_LoadsForAReaderThatIgnoresThem()
    {
        var document = Serializer.Deserialize(Serializer.Serialize(WithBounds("5")));

        document.Sections[0].Prompts[0].Response.Should().Be("5",
            "the response is readable whether or not the reader understands the bounds");
        new DocumentValidator().Validate(document).IsValid.Should().BeTrue();
    }
}
