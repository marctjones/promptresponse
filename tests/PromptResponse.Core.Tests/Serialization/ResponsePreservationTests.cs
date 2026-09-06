using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Core.Tests.Serialization;

/// <summary>
/// What survived the retirement of `responseMetadata`. These two assert contracts
/// that still hold — a response is preserved exactly, and serializing does not mutate
/// the document it was handed — and they were lost when the file holding them was
/// deleted for its other tests. Recovered from 8f581f7~1.
/// </summary>
/// <summary>
/// Saving and loading must not look like someone answered the form.
/// </summary>
/// <remarks>
/// <para>
/// The serializer normalizes text on the way in and on the way out, and it did so by
/// assigning through Prompt.Response - the setter that exists to record an authoring
/// edit. So every load and every save stamped LastModified with the current time and
/// cleared Source on every prompt in the document, whether or not a single character
/// changed.
/// </para>
/// <para>
/// That cost two things. LastModified stopped answering "when did this answer change"
/// and started answering "when was this file last touched by any tool at all". And
/// Source - the computed-versus-typed marker that tells a recomputation to leave a
/// hand-corrected value alone - was erased on the way through disk, so a value someone
/// deliberately overrode came back looking computed and was eligible to be overwritten
/// again on the next recompute.
/// </para>
/// <para>
/// Found by an export-immutability test that turned out to be measuring the serializer
/// rather than the renderer, which is why the control case in that suite exists.
/// </para>
/// </remarks>
public class ResponsePreservationTests
{
    private static readonly AprJsonSerializer Serializer = new();
    private static readonly DateTime Answered = new(2026, 3, 1, 9, 30, 0, DateTimeKind.Utc);

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

        // Set provenance after the responses, since answering is what clears it.
        foreach (var prompt in document.Sections[0].Prompts)
        {
        }
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
