using System.Text.Json.Nodes;
using AwesomeAssertions;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Models;
using Xunit;

namespace PromptResponse.Core.Tests.Beta6;

public sealed class SubmissionTargetTests
{
    private const string Form =
        "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\",\"submissionUrls\":["
        + "\"https://uploads.example.gov/a?X-Amz-Signature=1\","
        + "{\"kind\":\"post\",\"url\":\"https://uploads.example.gov/\",\"fields\":{\"key\":\"submissions/a\"},"
        + "\"expires\":\"2026-09-08T18:00:00Z\",\"refresh\":\"https://forms.example.gov/renew\",\"com.example.note\":\"kept\"},"
        + "{\"kind\":\"put\",\"url\":\"https://uploads.example.gov/b\"}"
        + "]},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\"}]}]}";

    private readonly AprBeta6Reader _reader = new();

    [Fact]
    public void AStringEntry_ReadsAsAPutTargetWithThatUrl()
    {
        // APR-MODEL-134, beside an object entry that states its own kind.
        var targets = _reader.ReadForm(Form, AprRepresentation.Jsonc).Metadata.SubmissionUrls!;

        targets[0].Kind.Should().Be(SubmissionTarget.Put);
        targets[0].Url.Should().Be("https://uploads.example.gov/a?X-Amz-Signature=1");
        targets[1].Kind.Should().Be(SubmissionTarget.Post);
        targets[1].Fields!.Value.GetProperty("key").GetString().Should().Be("submissions/a");
        targets[1].Expires.Should().Be("2026-09-08T18:00:00Z");
        targets[1].Refresh.Should().Be("https://forms.example.gov/renew");
    }

    [Theory]
    [InlineData(AprRepresentation.Jsonc)]
    [InlineData(AprRepresentation.Yaml)]
    public void EveryEntry_IsWrittenBackInTheSpellingItArrivedIn(AprRepresentation representation)
    {
        // A string entry stays a string, and an object keeps exactly the members it had,
        // an extension member included (APR-MODEL-021).
        var written = _reader.WriteForm(_reader.ReadForm(Form, AprRepresentation.Jsonc), representation);

        var again = _reader.ReadStream(written, representation).OfType<AprFormRecord>().Single().Value;
        JsonNode.DeepEquals(JsonNode.Parse(again.GetRawText()), JsonNode.Parse(Form))
            .Should().BeTrue($"reading and writing a form changes nothing in it, and wrote {written}");
    }
}
