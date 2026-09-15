using AwesomeAssertions;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Rendering;
using PromptResponse.Core.Validation;
using Xunit;

namespace PromptResponse.Core.Tests.Beta6;

public sealed class AttestationsNeverGateTheDataTests
{
    private const string Form =
        "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"Permit\"},\"sections\":[{\"id\":\"s\","
        + "\"title\":\"Applicant\",\"prompts\":[{\"id\":\"name\",\"label\":\"Name\",\"response\":\"Ada\"}]}]}";

    private static readonly string Zero = "sha256:" + new string('0', 64);

    public static TheoryData<string, string> Attestations => new()
    {
        { "absent", "" },
        { "of another form", Attestation("[]") },
        { "with a proof that does not verify", Attestation("[{\"type\":\"cms/ecdsa-p256-sha256\",\"value\":\"AAAA\"}]") },
        { "with an unrecognized proof", Attestation("[{\"type\":\"future/proof\",\"value\":\"x\"}]") },
    };

    [Theory]
    [MemberData(nameof(Attestations))]
    public void AForm_IsReadValidatedRenderedAndWritten_WhateverItsAttestation(string attestationState, string attestation)
    {
        // APR-ATTEST-011: saving needs no attestation. APR-ATTEST-012: parsing, validating,
        // rendering and extracting never wait on one. APR-ATTEST-019: an attestation's
        // presence, absence or state neither grants nor withholds the data.
        var reader = new AprBeta6Reader();
        var records = reader.ReadStream("" + Form + "\n" + attestation, AprRepresentation.Jsonc);
        foreach (var record in records.OfType<AprAttestationRecord>()) AprAttestationProofs.Verify(record);
        AprAttestationResolver.Resolve(records);

        var form = records.OfType<AprFormRecord>().Should().ContainSingle($"the attestation is {attestationState}").Subject.Form;
        new DocumentValidator().Validate(form).IsValid.Should().BeTrue();
        new DocumentRenderModelBuilder().Build(form, RenderOptions.Default).Blocks.OfType<FieldBlock>()
            .Should().ContainSingle().Which.Value.Should().Be("Ada");

        var written = reader.WriteStream(records, AprRepresentation.Jsonc);
        reader.ReadStream(written, AprRepresentation.Jsonc).OfType<AprFormRecord>().Single()
            .Form.Sections[0].Prompts[0].Response.Should().Be("Ada");
    }

    private static string Attestation(string proofs) =>
        "{\"recordType\":\"attestation\",\"aprVersion\":\"1.0-beta.6\","
        + $"\"subject\":{{\"digest\":\"{Zero}\",\"canonicalization\":\"jcs-sha256\"}},\"scope\":{{\"kind\":\"document\"}},"
        + $"\"manifest\":{{\"root\":\"{Zero}\",\"entries\":[{{\"path\":\"\",\"digest\":\"{Zero}\"}}]}},"
        + $"\"proofs\":{proofs},\"witnesses\":[]}}\n";
}
