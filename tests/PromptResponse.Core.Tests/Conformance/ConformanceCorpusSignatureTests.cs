using AwesomeAssertions;
using PromptResponse.Core.Signing;
using PromptResponse.Core.Validation;
using Xunit;

namespace PromptResponse.Core.Tests.Conformance;

/// <summary>Signature verification and canonical payload conformance checks.</summary>
public sealed class ConformanceCorpusSignatureTests : ConformanceCorpusTestBase
{
    public static IEnumerable<object[]> TamperedSignatureFiles() => CorpusFiles("signatures");

    [Theory]
    [MemberData(nameof(TamperedSignatureFiles))]
    public void TamperedSignatures_ValidateStructurally_ButFailVerification(string path)
    {
        var document = Serializer.Deserialize(File.ReadAllText(path));
        Validator.Validate(document).IsValid.Should().BeTrue(
            $"{Path.GetFileName(path)} is structurally valid; only its signature is broken");

        var results = AprVerifier.VerifyAll(document);
        results.Should().NotBeEmpty($"{Path.GetFileName(path)} carries at least one signature");
        results.Should().Contain(r => !r.ContentValid,
            $"{Path.GetFileName(path)} was tampered with and must fail verification");
    }

    [Fact]
    public void SignedFixture_VerifiesAsShipped()
    {
        var document = Serializer.Deserialize(File.ReadAllText(Path.Combine(CorpusDir("valid"), "signed-template.aprt")));
        AprVerifier.VerifyAll(document).Should().NotBeEmpty().And.OnlyContain(r => r.ContentValid,
            "the shipped signed fixture must verify over its own unmodified content");
    }

    [Fact]
    public void SubmissionUrlWithHiddenCharacters_IsReportedAndBlocksSigning()
    {
        var document = Serializer.Deserialize(File.ReadAllText(Path.Combine(CorpusDir("valid"), "signed-template.aprt")));
        document.Signatures = null;
        document.Metadata.SubmissionUrls = ["https://bloomfield\u200bct.gov/submit"];

        Validator.Validate(document).IsValid.Should().BeTrue();
        new HiddenCharacterAdvisor().Validate(document).Warnings
            .Should().Contain(w => w.WarningCode == "SUBMISSION_URL_HIDDEN_CHARS");

        using var cert = SignatureCertificates.CreateSelfSigned(
            "Conformance", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var sign = () => AprSigner.SignTemplate(document, cert, DateTime.UtcNow);
        sign.Should().Throw<InvalidOperationException>(
            "binding a URL that renders as one host and resolves as another defeats the binding");
    }

    [Theory]
    [InlineData("formDefinition")]
    [InlineData("publisherPayload")]
    [InlineData("fillerPayload")]
    public void CanonicalPayload_MatchesThePublishedVector(string vectorName)
    {
        var dir = CorpusDir("canonicalization");
        using var vectors = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "vectors.json")));
        var parameters = vectors.RootElement.GetProperty("parameters");
        var expected = vectors.RootElement.GetProperty("vectors").GetProperty(vectorName);
        var document = Serializer.Deserialize(File.ReadAllText(Path.Combine(dir, "input.aprt")));
        var signedAt = parameters.GetProperty("signedAt").GetString()!;
        var fields = parameters.GetProperty("fillerFields").EnumerateArray().Select(f => f.GetString()!).ToArray();
        var actual = vectorName switch
        {
            "formDefinition" => AprCanonicalizer.FormDefinition(document),
            "publisherPayload" => AprCanonicalizer.PublisherPayload(document, signedAt),
            "fillerPayload" => AprCanonicalizer.FillerPayload(document, fields, signedAt),
            _ => throw new ArgumentOutOfRangeException(nameof(vectorName)),
        };

        System.Text.Encoding.UTF8.GetString(actual).Should().Be(expected.GetProperty("canonicalText").GetString(),
            $"the {vectorName} canonical payload is a published interop contract");
        actual.Length.Should().Be(expected.GetProperty("byteLength").GetInt32());
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(actual)).ToLowerInvariant()
            .Should().Be(expected.GetProperty("sha256").GetString());
    }
}
