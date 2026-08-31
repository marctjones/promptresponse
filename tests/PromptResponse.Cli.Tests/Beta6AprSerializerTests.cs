using AwesomeAssertions;
using PromptResponse.Cli;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Cli.Tests;

public sealed class Beta6AprSerializerTests
{
    private readonly Beta6AprSerializer _serializer = new();

    [Fact]
    public void Deserialize_AcceptsOnlyOneUnsignedBeta6Form()
    {
        _serializer.Deserialize("""{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[]}]}""").Metadata.Title.Should().Be("T");

        var beta3 = () => _serializer.Deserialize("""{"version":"1.0-beta","metadata":{"title":"T"},"sections":[]}""");
        beta3.Should().Throw<SerializationException>().WithMessage("*1.0-beta.6*");

        var signed = () => _serializer.Deserialize("""{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":[],"signatures":[]}""");
        signed.Should().Throw<SerializationException>().WithMessage("*RETIRED_EMBEDDED_SIGNATURES*");
    }
}
