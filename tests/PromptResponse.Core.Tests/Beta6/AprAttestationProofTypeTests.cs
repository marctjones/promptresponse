using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using AwesomeAssertions;
using PromptResponse.Core.Beta6;
using Xunit;

namespace PromptResponse.Core.Tests.Beta6;

/// <summary>
/// APR-ATTEST-037: the proof type <c>cms/ecdsa-p256-sha256</c> names an algorithm. A CMS
/// signature that verifies but was made another way is a real signature that is not that proof
/// type, so it must not verify as one. These sign the same payload the correct way and three
/// wrong ways and ask the verifier about each.
/// </summary>
public class AprAttestationProofTypeTests
{
    private const string Envelope = """
        {"recordType":"attestation","aprVersion":"1.0-beta.6",
         "subject":{"digest":"sha256:0000000000000000000000000000000000000000000000000000000000000000","canonicalization":"jcs-sha256"},
         "scope":{"kind":"document"},
         "manifest":{"root":"sha256:0000000000000000000000000000000000000000000000000000000000000000","entries":[]},
         "proofs":[],"witnesses":[]}
        """;

    private static AprAttestationRecord Record(string proofValue)
    {
        using var envelope = JsonDocument.Parse(Envelope);
        var members = envelope.RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => (object)p.Value.Clone());
        members["proofs"] = new[] { new { type = AprAttestationProofs.CmsEcdsaP256Sha256, value = proofValue } };
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(members));
        return new AprAttestationRecord(document.RootElement.Clone());
    }

    private static byte[] Payload()
    {
        using var envelope = JsonDocument.Parse(Envelope);
        return AprAttestationProofs.SigningPayload(new AprAttestationRecord(envelope.RootElement.Clone()));
    }

    private static X509Certificate2 Ecdsa(ECCurve curve, HashAlgorithmName hash)
    {
        var key = ECDsa.Create(curve);
        var request = new CertificateRequest("CN=signer", key, hash);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private static X509Certificate2 Rsa()
    {
        var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=signer", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private static string Sign(X509Certificate2 certificate, Oid? digest = null, params X509Certificate2[] more)
    {
        var cms = new SignedCms(new ContentInfo(Payload()), detached: true);
        var signer = new CmsSigner(certificate);
        if (digest is not null) signer.DigestAlgorithm = digest;
        cms.ComputeSignature(signer);
        foreach (var other in more) cms.ComputeSignature(new CmsSigner(other));
        return Convert.ToBase64String(cms.Encode());
    }

    private static AprAttestationProofVerification Verify(string proofValue)
        => AprAttestationProofs.Verify(Record(proofValue)).Single();

    [Fact]
    public void EcdsaP256WithSha256_Verifies()
    {
        using var certificate = Ecdsa(ECCurve.NamedCurves.nistP256, HashAlgorithmName.SHA256);

        Verify(Sign(certificate)).ContentValid.Should().BeTrue("this is the algorithm the proof type names");
    }

    [Fact]
    public void ARsaSignature_IsNotThisProofType()
    {
        using var certificate = Rsa();

        var result = Verify(Sign(certificate));

        result.ContentValid.Should().BeFalse("the type names ECDSA, and a valid RSA CMS signature is not that");
        result.Status.Should().Contain("ECDSA");
    }

    [Fact]
    public void AnEcdsaP384Key_IsNotThisProofType()
    {
        using var certificate = Ecdsa(ECCurve.NamedCurves.nistP384, HashAlgorithmName.SHA384);

        var result = Verify(Sign(certificate));

        result.ContentValid.Should().BeFalse();
        result.Status.Should().Contain("P-256");
    }

    [Fact]
    public void ASha384Digest_IsNotThisProofType()
    {
        using var certificate = Ecdsa(ECCurve.NamedCurves.nistP256, HashAlgorithmName.SHA256);

        var result = Verify(Sign(certificate, new Oid("2.16.840.1.101.3.4.2.2")));

        result.ContentValid.Should().BeFalse();
        result.Status.Should().Contain("SHA-256");
    }

    [Fact]
    public void ASecondSigner_IsNotThisProofType_BecauseItsTrustWouldGoUnexamined()
    {
        using var first = Ecdsa(ECCurve.NamedCurves.nistP256, HashAlgorithmName.SHA256);
        using var second = Rsa();

        var result = Verify(Sign(first, null, second));

        result.ContentValid.Should().BeFalse();
        result.Status.Should().Contain("signers");
    }
}
