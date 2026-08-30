using AwesomeAssertions;
using PromptResponse.Core.Rendering;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Core.Tests.Conformance;

/// <summary>Valid, invalid, and malformed corpus contract checks.</summary>
public sealed class ConformanceCorpusValidityTests : ConformanceCorpusTestBase
{
    public static IEnumerable<object[]> ValidCorpusFiles() => CorpusFiles("valid");
    public static IEnumerable<object[]> InvalidCorpusFiles() => CorpusFiles("invalid");
    public static IEnumerable<object[]> MalformedCorpusFiles() => CorpusFiles("malformed");

    [Theory]
    [MemberData(nameof(ValidCorpusFiles))]
    public void ValidCorpus_Deserializes_Validates_AndRoundTrips(string path)
    {
        var originalJson = File.ReadAllText(path);
        var document = Serializer.Deserialize(originalJson);
        var result = Validator.Validate(document);

        result.IsValid.Should().BeTrue($"{Path.GetFileName(path)} is a valid conformance fixture");
        result.Errors.Should().BeEmpty();
        document.Sections.Should().NotBeEmpty();

        var roundTripped = Serializer.Deserialize(Serializer.Serialize(document));
        var roundTripResult = Validator.Validate(roundTripped);
        roundTripResult.IsValid.Should().BeTrue($"{Path.GetFileName(path)} must stay valid after serialize/deserialize");
        roundTripResult.Errors.Should().BeEmpty();

        new DocumentRenderModelBuilder().Build(roundTripped, RenderOptions.Default).Blocks
            .Should().NotBeEmpty($"{Path.GetFileName(path)} should produce renderable output");
        ResponsesById(document).Should().Equal(ResponsesById(roundTripped),
            $"{Path.GetFileName(path)} must preserve every response byte-for-byte across a round-trip");
    }

    [Theory]
    [MemberData(nameof(InvalidCorpusFiles))]
    public void InvalidCorpus_Deserializes_ButDoesNotValidate(string path)
    {
        var result = Validator.Validate(Serializer.Deserialize(File.ReadAllText(path)));
        result.IsValid.Should().BeFalse($"{Path.GetFileName(path)} is intentionally invalid");
        result.Errors.Should().NotBeEmpty();
    }

    [Theory]
    [MemberData(nameof(MalformedCorpusFiles))]
    public void MalformedCorpus_IsRejectedAtParseTime(string path)
    {
        var parse = () => Serializer.Deserialize(File.ReadAllText(path));
        parse.Should().Throw<SerializationException>(
            $"{Path.GetFileName(path)} is malformed and must not be silently coerced");
    }

    private static SortedDictionary<string, string> ResponsesById(Core.Models.AprDocument document)
    {
        var responses = new SortedDictionary<string, string>(StringComparer.Ordinal);
        void Walk(Core.Models.Section section)
        {
            foreach (var prompt in section.Prompts) responses[prompt.Id] = prompt.Response;
            foreach (var child in section.Sections) Walk(child);
        }
        foreach (var section in document.Sections) Walk(section);
        return responses;
    }
}
