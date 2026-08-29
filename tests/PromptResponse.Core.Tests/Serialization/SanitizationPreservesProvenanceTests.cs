using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Core.Tests.Serialization;

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
public class SanitizationPreservesProvenanceTests
{
    private static readonly AprJsonSerializer Serializer = new();
    private static readonly DateTime Answered = new(2026, 3, 1, 9, 30, 0, DateTimeKind.Utc);

    private static AprDocument Filled()
    {
        var document = new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata { Title = "Expenses", TemplateId = "expense", TemplateVersion = "1.0" },
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
            prompt.ResponseMetadata.LastModified = Answered;
        }
        document.Sections[0].Prompts[1].ResponseMetadata.Source = "computed";
        return document;
    }

    [Fact]
    public void Serializing_DoesNotRestampLastModified()
    {
        var document = Filled();

        Serializer.Serialize(document);

        foreach (var prompt in document.Sections[0].Prompts)
        {
            prompt.ResponseMetadata.LastModified.Should().Be(Answered,
                "saving a form is not answering it; lastModified must say when the answer " +
                "changed, not when the file was last written");
        }
    }

    [Fact]
    public void Serializing_DoesNotEraseProvenance()
    {
        var document = Filled();

        Serializer.Serialize(document);

        document.Sections[0].Prompts[1].ResponseMetadata.Source.Should().Be("computed",
            "the computed-versus-typed marker must survive a save, because it is what stops " +
            "a recomputation from overwriting a value someone corrected by hand");
    }

    [Fact]
    public void RoundTrip_PreservesLastModifiedAndSource()
    {
        var reloaded = Serializer.Deserialize(Serializer.Serialize(Filled()));

        var prompts = reloaded.Sections[0].Prompts;
        prompts[0].ResponseMetadata.LastModified.Should().Be(Answered);
        prompts[1].ResponseMetadata.LastModified.Should().Be(Answered);
        prompts[1].ResponseMetadata.Source.Should().Be("computed",
            "a value a person overrode must not come back looking computed after a trip " +
            "through disk, or the next recompute silently reverts their correction");
    }

    [Fact]
    public void SerializingTwice_ProducesIdenticalBytes()
    {
        var document = Filled();

        var first = Serializer.Serialize(document);

        Serializer.Serialize(document).Should().Be(first,
            "serializing must not mutate the document it was handed");
    }

    /// <summary>Answering, unlike saving, must still record the edit.</summary>
    /// <remarks>
    /// The fix must not go too far the other way: a real edit still has to stamp the time
    /// and mark the value as authored, or the provenance it was protecting means nothing.
    /// </remarks>
    [Fact]
    public void AnsweringStillRecordsTheEdit()
    {
        var document = Filled();
        var prompt = document.Sections[0].Prompts[1];

        prompt.Response = "40.00";

        prompt.ResponseMetadata.LastModified.Should().NotBe(Answered,
            "a real edit must still update the timestamp");
        prompt.ResponseMetadata.Source.Should().BeNull(
            "typing over a computed value must mark it as authored, so the next " +
            "recomputation leaves it alone");
    }

    /// <summary>Responses must survive exactly - including their normalization form.</summary>
    /// <remarks>
    /// An earlier draft normalized this response, but that changes evidence just as
    /// surely as stripping a zero-width space. Responses are what a person typed;
    /// the serializer leaves normalization and suspicious characters intact for
    /// HiddenCharacterAdvisor to report rather than quietly rewriting an answer.
    /// </remarks>
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
