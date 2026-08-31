using System.Text.Json;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Signing;
using Xunit;

namespace PromptResponse.Core.Tests.Beta6;

public class AprAttestationResolverTests
{
    private readonly AprBeta6Reader _reader = new();

    [Fact]
    public void MatchingSubjectAndManifest_AreUnverifiableWithoutAProof()
    {
        var form = FormRecord();
        var manifest = AprSemanticDigest.CreateManifest(form.Value);
        var attestation = Attestation(AprSemanticDigest.Digest(form.Value), manifest.Root, manifest.Entries);

        var result = AprAttestationResolver.Resolve([form, attestation]).Single();

        result.State.Should().Be(AprAttestationState.Unverifiable);
        result.DifferingPaths.Should().BeEmpty();
    }

    [Fact]
    public void MismatchedManifest_IsInvalidButNeverRemovesTheForm()
    {
        var form = FormRecord();
        var attestation = Attestation(AprSemanticDigest.Digest(form.Value), "sha256:0000000000000000000000000000000000000000000000000000000000000000", []);

        var result = AprAttestationResolver.Resolve([form, attestation]).Single();

        result.State.Should().Be(AprAttestationState.Invalid);
        result.DifferingPaths.Should().Contain("");
        form.Form.Metadata.Title.Should().Be("T");
    }

    [Fact]
    public void UnsupportedProof_IsUnverifiableRatherThanInvalidatingTheForm()
    {
        var form = FormRecord();
        var manifest = AprSemanticDigest.CreateManifest(form.Value);
        var attestation = Attestation(AprSemanticDigest.Digest(form.Value), manifest.Root, manifest.Entries, "future/proof");

        var proof = AprAttestationProofs.Verify(attestation).Single();

        proof.ContentValid.Should().BeFalse();
        proof.Status.Should().Contain("unverifiable");
        form.Form.Sections.Should().NotBeEmpty();
    }

    [Fact]
    public void ValidCmsProof_MakesTheResolvedAttestationValid()
    {
        var form = FormRecord();
        var manifest = AprSemanticDigest.CreateManifest(form.Value);
        var unsigned = Attestation(AprSemanticDigest.Digest(form.Value), manifest.Root, manifest.Entries, AprAttestationProofs.CmsEcdsaP256Sha256);
        using var certificate = SignatureCertificates.CreateSelfSigned("Ada", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var signed = Attestation(AprSemanticDigest.Digest(form.Value), manifest.Root, manifest.Entries,
            AprAttestationProofs.CmsEcdsaP256Sha256, Sign(AprAttestationProofs.SigningPayload(unsigned), certificate));

        var result = AprAttestationResolver.Resolve([form, signed]).Single();

        result.State.Should().Be(AprAttestationState.Valid);
    }

    [Fact]
    public void Factory_CreatesIndependentCmsAttestationWithoutMutatingTheForm()
    {
        var form = FormRecord();
        using var certificate = SignatureCertificates.CreateSelfSigned("Ada", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        var attestation = AprAttestationFactory.Create(form.Value, certificate, ["p"]);

        attestation.Value.GetProperty("scope").GetProperty("kind").GetString().Should().Be("fields");
        AprAttestationProofs.Verify(attestation).Single().ContentValid.Should().BeTrue();
        AprAttestationResolver.Resolve([form, attestation]).Single().State.Should().Be(AprAttestationState.Valid);
        form.Value.TryGetProperty("signatures", out _).Should().BeFalse();
    }

    [Fact]
    public void FieldsScope_MustCarryPromptResponseAndSectionContext()
    {
        var form = FormRecord();
        var manifest = AprSemanticDigest.CreateManifest(form.Value);
        var missingResponse = manifest.Entries.Where(entry => entry.Path != "/sections/0/prompts/0/response").ToList();
        var json = JsonSerializer.Serialize(new
        {
            recordType = "attestation", version = "1.0-beta.6",
            subject = new { digest = AprSemanticDigest.Digest(form.Value), canonicalization = "jcs-sha256" },
            scope = new { kind = "fields", fields = new[] { "p" } },
            manifest = new { root = manifest.Root, entries = missingResponse.Select(entry => new { path = entry.Path, digest = entry.Digest }) },
            proofs = Array.Empty<object>(), witnesses = Array.Empty<string>(),
        });
        using var parsed = JsonDocument.Parse(json);

        var result = AprAttestationResolver.Resolve([form, new AprAttestationRecord(parsed.RootElement.Clone())]).Single();

        result.State.Should().Be(AprAttestationState.Invalid);
        result.DifferingPaths.Should().Contain("/sections/0/prompts/0/response");
    }

    private AprFormRecord FormRecord() => (AprFormRecord)_reader.ReadStream("""
        {"version":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P","response":"Ada"}]}]}
        """, AprRepresentation.Jsonc).Single();

    private static AprAttestationRecord Attestation(string subject, string root, IReadOnlyList<AprManifestEntry> entries, string? proofType = null, string proofValue = "")
    {
        var json = JsonSerializer.Serialize(new
        {
            recordType = "attestation", version = "1.0-beta.6",
            subject = new { digest = subject, canonicalization = "jcs-sha256" },
            scope = new { kind = "document" },
            manifest = new { root, entries = entries.Select(entry => new { path = entry.Path, digest = entry.Digest }) },
            proofs = proofType is null ? Array.Empty<object>() : new[] { new { type = proofType, value = proofValue } },
            witnesses = Array.Empty<string>(),
        });
        using var document = JsonDocument.Parse(json);
        return new AprAttestationRecord(document.RootElement.Clone());
    }

    private static string Sign(byte[] payload, X509Certificate2 certificate)
    {
        var cms = new SignedCms(new ContentInfo(payload), detached: true);
        cms.ComputeSignature(new CmsSigner(certificate));
        return Convert.ToBase64String(cms.Encode());
    }
}
