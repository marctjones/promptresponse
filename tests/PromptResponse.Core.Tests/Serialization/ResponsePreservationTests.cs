using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Core.Tests.Serialization;

/// <summary>
/// Saving and loading must not look like someone answered the form: a response is
/// preserved exactly, and serializing does not mutate the document it was handed.
/// </summary>
public class ResponsePreservationTests
{
    private static readonly AprJsonSerializer Serializer = new();

    private static AprDocument Filled()
    {
        var document = new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata { Title = "Expenses", TemplateId = "tag:skpt.cl,2026:tests/expense", TemplateVersion = "1.0" },
            Sections =
            [
                new Section
                {
                    Id = "s", Title = "Claim",
                    Prompts =
                    [
                        new Prompt { Id = "typed", Label = "Description", Response = "Taxi from the airport" },
                        new Prompt { Id = "computed", Label = "Total", Response = "42.00" },
                    ],
                },
            ],
        };

        return document;
    }

    [Fact]
    public void SerializingTwice_ProducesIdenticalBytes()
    {
        var document = Filled();

        var first = Serializer.Serialize(document);

        Serializer.Serialize(document).Should().Be(first,
            "serializing must not mutate the document it was handed");
    }

    [Fact]
    public void SanitizationPreservesExactResponseWithoutRewritingWhatSomeoneTyped()
    {
        var document = Filled();
        // "Cafe" + combining acute: deliberately preserve this decomposed spelling.
        document.Sections[0].Prompts[0].Response = "Café receipt​";

        var reloaded = Serializer.Deserialize(Serializer.Serialize(document));
        var response = reloaded.Sections[0].Prompts[0].Response;

        response.Should().Be("Café receipt​",
            "a response is exact evidence, including its original normalization form");
        response.Should().Contain("​",
            "a zero-width space in a response is left alone and reported, not silently " +
            "removed; a response is what a person typed, not something to correct");
    }
}
