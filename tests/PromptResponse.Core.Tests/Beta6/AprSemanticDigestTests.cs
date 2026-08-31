using System.Text.Json;
using AwesomeAssertions;
using PromptResponse.Core.Beta6;
using Xunit;

namespace PromptResponse.Core.Tests.Beta6;

public class AprSemanticDigestTests
{
    [Fact]
    public void Digest_IsIndependentOfJsonObjectOrder()
    {
        using var first = JsonDocument.Parse("{\"b\":2,\"a\":{\"z\":true,\"x\":\"value\"}}");
        using var second = JsonDocument.Parse("{\"a\":{\"x\":\"value\",\"z\":true},\"b\":2.0}");

        AprSemanticDigest.Digest(first.RootElement).Should().Be(AprSemanticDigest.Digest(second.RootElement));
    }

    [Fact]
    public void Manifest_ContainsRootAndNonPlaintextLeafDigest()
    {
        using var form = JsonDocument.Parse("""
            {"version":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P","response":"Ada"}]}]}
            """);

        var manifest = AprSemanticDigest.CreateManifest(form.RootElement);

        manifest.Root.Should().Be(AprSemanticDigest.Digest(form.RootElement));
        manifest.Entries.Should().Contain(entry => entry.Path == "");
        manifest.Entries.Should().Contain(entry => entry.Path == "/sections/0/prompts/0/response" && entry.Digest != "Ada");
    }
}
