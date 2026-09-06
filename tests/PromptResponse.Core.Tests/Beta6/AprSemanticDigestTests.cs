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

    /// <summary>RFC 8785 Appendix B: IEEE 754 bit patterns and their required JCS spellings.</summary>
    [Theory]
    [InlineData("0000000000000000", "0")]
    [InlineData("8000000000000000", "0")]
    [InlineData("0000000000000001", "5e-324")]
    [InlineData("0000000000000002", "1e-323")]
    [InlineData("8000000000000001", "-5e-324")]
    [InlineData("7fefffffffffffff", "1.7976931348623157e+308")]
    [InlineData("ffefffffffffffff", "-1.7976931348623157e+308")]
    [InlineData("4340000000000000", "9007199254740992")]
    [InlineData("c340000000000000", "-9007199254740992")]
    [InlineData("4430000000000000", "295147905179352830000")]
    [InlineData("44b52d02c7e14af5", "9.999999999999997e+22")]
    [InlineData("44b52d02c7e14af6", "1e+23")]
    [InlineData("44b52d02c7e14af7", "1.0000000000000001e+23")]
    [InlineData("444b1ae4d6e2ef4e", "999999999999999700000")]
    [InlineData("444b1ae4d6e2ef4f", "999999999999999900000")]
    [InlineData("444b1ae4d6e2ef50", "1e+21")]
    [InlineData("3eb0c6f7a0b5ed8c", "9.999999999999997e-7")]
    [InlineData("3eb0c6f7a0b5ed8d", "0.000001")]
    [InlineData("41b3de4355555553", "333333333.3333332")]
    [InlineData("41b3de4355555554", "333333333.33333325")]
    [InlineData("41b3de4355555555", "333333333.3333333")]
    [InlineData("41b3de4355555556", "333333333.3333334")]
    [InlineData("41b3de4355555557", "333333333.33333343")]
    [InlineData("becbf647612f3696", "-0.0000033333333333333333")]
    [InlineData("43143ff3c1cb0959", "1424953923781206.2")]
    public void CanonicalNumber_MatchesRfc8785AppendixB(string bits, string expected)
    {
        var number = BitConverter.Int64BitsToDouble(unchecked((long)Convert.ToUInt64(bits, 16)));

        AprSemanticDigest.CanonicalNumber(number).Should().Be(expected);
    }

    [Theory]
    [InlineData("1996", "1996")]
    [InlineData("1996.0", "1996")]
    [InlineData("5", "5")]
    [InlineData("-0", "0")]
    [InlineData("1e21", "1e+21")]
    [InlineData("1e20", "100000000000000000000")]
    [InlineData("1E-7", "1e-7")]
    [InlineData("0.000001", "0.000001")]
    [InlineData("9007199254740994", "9007199254740994")]
    public void Canonicalize_WritesNumbersAsEs6NumberToString(string json, string expected)
    {
        using var document = JsonDocument.Parse(json);

        System.Text.Encoding.UTF8.GetString(AprSemanticDigest.Canonicalize(document.RootElement)).Should().Be(expected);
    }

    [Fact]
    public void Digest_IsIndependentOfIntegralNumberSpelling()
    {
        using var integral = JsonDocument.Parse("{\"maxRows\":5,\"min\":1996,\"canAddRows\":true}");
        using var fractional = JsonDocument.Parse("{\"canAddRows\":true,\"min\":1.996e3,\"maxRows\":5.0}");

        System.Text.Encoding.UTF8.GetString(AprSemanticDigest.Canonicalize(fractional.RootElement))
            .Should().Be("{\"canAddRows\":true,\"maxRows\":5,\"min\":1996}");
        AprSemanticDigest.Digest(integral.RootElement).Should().Be(AprSemanticDigest.Digest(fractional.RootElement));
    }

    [Fact]
    public void Manifest_ContainsRootAndNonPlaintextLeafDigest()
    {
        using var form = JsonDocument.Parse("""
            {"aprVersion":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P","response":"Ada"}]}]}
            """);

        var manifest = AprSemanticDigest.CreateManifest(form.RootElement);

        manifest.Root.Should().Be(AprSemanticDigest.Digest(form.RootElement));
        manifest.Entries.Should().Contain(entry => entry.Path == "");
        manifest.Entries.Should().Contain(entry => entry.Path == "/sections/0/prompts/0/response" && entry.Digest != "Ada");
    }
}
