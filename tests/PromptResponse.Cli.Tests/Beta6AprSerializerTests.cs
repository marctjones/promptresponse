using AwesomeAssertions;
using PromptResponse.Cli;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Cli.Tests;

public sealed class Beta6AprSerializerTests
{
    private readonly Beta6AprSerializer _serializer = new();

    [Fact]
    public void Deserialize_AcceptsOnlyABeta6Form()
    {
        _serializer.Deserialize("""{"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[]}]}""").Metadata.Title.Should().Be("T");

        var beta3 = () => _serializer.Deserialize("""{"aprVersion":"1.0-beta.3","metadata":{"title":"T"},"sections":[]}""");
        beta3.Should().Throw<SerializationException>().Which.Code.Should().Be("UNSUPPORTED_VERSION");
    }

    [Fact]
    public void Deserialize_FallsBackToYaml_WhenTheContentIsNotJsonc()
    {
        // Nothing tells this serializer which representation a string is in (the
        // IAprSerializer contract has no such parameter), so every command built on
        // it -- fill, new, validate's underlying reader aside, diff, submit, and more
        // -- could open only JSONC until 2026-09-07: a .apr.yaml file crashed with a
        // JSON parse error before this fallback existed.
        var yaml = new AprBeta6Reader().WriteForm(
            new PromptResponse.Core.Models.AprDocument
            {
                Version = PromptResponse.Core.AprFormat.CurrentVersion,
                Metadata = new PromptResponse.Core.Models.Metadata { Title = "YAML form" },
                Sections = [],
            },
            AprRepresentation.Yaml);

        _serializer.Deserialize(yaml).Metadata.Title.Should().Be("YAML form");
    }

    [Fact]
    public void Deserialize_StillThrowsAprStreamRequiresIteration_ForAMultiRecordJsoncStream()
    {
        // A JSONC document that parses fine but turns out to hold more than one
        // record is a real answer (iterate it), not a "maybe this is YAML" guess --
        // the fallback above must not swallow it.
        const string stream =
            "\x1e{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"A\"},\"sections\":[]}\n"
            + "\x1e{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"B\"},\"sections\":[]}\n";

        var act = () => _serializer.Deserialize(stream);

        act.Should().Throw<AprStreamRequiresIterationException>();
    }

    [Fact]
    public void Deserialize_ReportsAParseError_WhenContentIsNeitherJsoncNorYaml()
    {
        // Doesn't start with '{', so the JSONC failure below triggers a YAML retry --
        // and this is not a valid APR document under either representation (a bare
        // plain scalar, not the required top-level mapping), so both must fail and
        // the caller must still see a real parse error rather than a silent success.
        var act = () => _serializer.Deserialize("just a plain scalar, not an APR document at all");

        act.Should().Throw<SerializationException>();
    }
}
