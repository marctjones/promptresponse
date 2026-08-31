using AwesomeAssertions;
using PromptResponse.Core.Beta6;
using Xunit;

namespace PromptResponse.Core.Tests.Beta6;

/// <summary>Runs the shared beta.6 corpus through the reference public boundary.</summary>
public sealed class AprBeta6CorpusTests
{
    private readonly AprBeta6Reader _reader = new();
    private static string Corpus => Path.Combine(
        Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..")),
        "tests", "Conformance", "beta6");

    [Fact]
    public void PairedFormsAndStreams_FollowTheSharedBeta6Corpus()
    {
        var jsonc = _reader.ReadForm(File.ReadAllText(Path.Combine(Corpus, "forms", "permit.apr.jsonc")), AprRepresentation.Jsonc);
        var yaml = _reader.ReadForm(File.ReadAllText(Path.Combine(Corpus, "forms", "permit.apr.yaml")), AprRepresentation.Yaml);
        jsonc.Metadata.Title.Should().Be(yaml.Metadata.Title);
        jsonc.Sections.Single().Prompts.Single().Response.Should().Be(yaml.Sections.Single().Prompts.Single().Response);

        var outOfOrder = _reader.ReadStream(File.ReadAllText(Path.Combine(Corpus, "streams", "out-of-order.apr.jsonc")), AprRepresentation.Jsonc);
        AprAttestationResolver.Resolve(outOfOrder).Single().State.Should().Be(AprAttestationState.Unverifiable);
        var yamlOutOfOrder = _reader.ReadStream(File.ReadAllText(Path.Combine(Corpus, "streams", "out-of-order.apr.yaml")), AprRepresentation.Yaml);
        yamlOutOfOrder.OfType<AprFormRecord>().Should().HaveCount(2);
        AprAttestationResolver.Resolve(yamlOutOfOrder).Single().State.Should().Be(AprAttestationState.Unverifiable);

        var witnessed = _reader.ReadStream(File.ReadAllText(Path.Combine(Corpus, "streams", "witnessed.apr.jsonc")), AprRepresentation.Jsonc);
        AprAttestationResolver.Resolve(witnessed)[1].WitnessesResolved.Should().Be(1);
        var witnessChain = _reader.ReadStream(File.ReadAllText(Path.Combine(Corpus, "streams", "witness-chain.apr.jsonc")), AprRepresentation.Jsonc);
        AprAttestationResolver.Resolve(witnessChain)[1].WitnessesResolved.Should().Be(1);
        AprAttestationResolver.Resolve(witnessChain)[2].WitnessesResolved.Should().Be(1);

        var changed = _reader.ReadStream(File.ReadAllText(Path.Combine(Corpus, "streams", "changed-form.apr.jsonc")), AprRepresentation.Jsonc);
        AprAttestationResolver.Resolve(changed).Single().State.Should().Be(AprAttestationState.Unresolved);

        var fields = (AprAttestationRecord)_reader.ReadStream(File.ReadAllText(Path.Combine(Corpus, "attestations", "permit.fields.attestation.jsonc")), AprRepresentation.Jsonc).Single();
        AprAttestationResolver.Resolve([outOfOrder.OfType<AprFormRecord>().First(), fields]).Single().State.Should().Be(AprAttestationState.Unverifiable);
        var unsupported = (AprAttestationRecord)_reader.ReadStream(File.ReadAllText(Path.Combine(Corpus, "attestations", "permit.unsupported.attestation.jsonc")), AprRepresentation.Jsonc).Single();
        AprAttestationResolver.Resolve([outOfOrder.OfType<AprFormRecord>().First(), unsupported]).Single().State.Should().Be(AprAttestationState.Unverifiable);
    }

    [Fact]
    public void MalformedSharedStream_IsRejected()
    {
        foreach (var path in Directory.GetFiles(Path.Combine(Corpus, "malformed")))
        {
            var representation = Path.GetExtension(path) is ".yaml" or ".yml" ? AprRepresentation.Yaml : AprRepresentation.Jsonc;
            var read = () => _reader.ReadStream(File.ReadAllText(path), representation);
            read.Should().Throw<Exception>(Path.GetFileName(path));
        }
    }

    [Fact]
    public void CmsCorpusProof_BindsTheExactProofFreeEnvelope()
    {
        var form = (AprFormRecord)_reader.ReadStream(File.ReadAllText(Path.Combine(Corpus, "forms", "permit.apr.jsonc")), AprRepresentation.Jsonc).Single();
        var attestation = (AprAttestationRecord)_reader.ReadStream(File.ReadAllText(Path.Combine(Corpus, "attestations", "permit.cms.attestation.jsonc")), AprRepresentation.Jsonc).Single();

        AprAttestationProofs.Verify(attestation).Single().ContentValid.Should().BeTrue();
        AprAttestationResolver.Resolve([form, attestation]).Single().State.Should().Be(AprAttestationState.Valid);

        var changed = File.ReadAllText(Path.Combine(Corpus, "attestations", "permit.cms.attestation.jsonc"))
            .Replace("\"kind\": \"document\"", "\"kind\": \"fields\", \"fields\": [\"name\"]", StringComparison.Ordinal);
        var tampered = (AprAttestationRecord)_reader.ReadStream(changed, AprRepresentation.Jsonc).Single();
        AprAttestationProofs.Verify(tampered).Single().ContentValid.Should().BeFalse();
    }
}
