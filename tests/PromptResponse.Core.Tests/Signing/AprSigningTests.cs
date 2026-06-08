using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Signing;
using Xunit;

namespace PromptResponse.Core.Tests.Signing;

/// <summary>
/// Verifies apr-sig-v2: detached CMS/PKCS#7 signatures over canonical APR content
/// with X.509 certificates. Covers tamper detection, scope isolation, URL binding,
/// JSON round-trip, and both trust models (self-signed/pinned and CA-issued).
/// </summary>
public class AprSigningTests
{
    private static readonly DateTime At = new(2026, 6, 8, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset Before = DateTimeOffset.UtcNow.AddDays(-1);
    private static readonly DateTimeOffset After = DateTimeOffset.UtcNow.AddYears(2);

    private static X509Certificate2 SelfSigned(string cn = "Town of Bloomfield") =>
        SignatureCertificates.CreateSelfSigned(cn, Before, After);

    private static AprDocument Template() => new()
    {
        DocumentType = DocumentType.Template,
        Metadata = new Metadata { Title = "Permit", TemplateId = "permit", TemplateVersion = "1.0" },
        Sections =
        [
            new Section
            {
                Id = "s", Title = "Applicant",
                Prompts =
                [
                    new Prompt { Id = "name", Label = "Name" },
                    new Prompt { Id = "dob", Label = "Date of Birth" },
                    new Prompt { Id = "notes", Label = "Notes" },
                ],
            },
        ],
    };

    // ── Publisher ───────────────────────────────────────────────────────────

    [Fact]
    public void Publisher_SignsTemplate_ContentValid()
    {
        var doc = Template();
        using var cert = SelfSigned();
        doc.Signatures = [AprSigner.SignTemplate(doc, cert, "https://bloomfieldct.gov/permit/submit", At)];

        var r = AprVerifier.Verify(doc, doc.Signatures[0]);

        r.ContentValid.Should().BeTrue();
        r.Role.Should().Be(SignatureRole.Publisher);
        r.SignerName.Should().Be("Town of Bloomfield");
    }

    [Fact]
    public void Publisher_DetectsFormDefinitionTampering()
    {
        var doc = Template();
        using var cert = SelfSigned();
        doc.Signatures = [AprSigner.SignTemplate(doc, cert, "https://x/submit", At)];

        doc.Sections[0].Prompts[0].Label = "Full Name";

        AprVerifier.Verify(doc, doc.Signatures[0]).ContentValid.Should().BeFalse();
    }

    [Fact]
    public void Publisher_BindsSubmissionUrl()
    {
        var doc = Template();
        using var cert = SelfSigned();
        doc.Signatures = [AprSigner.SignTemplate(doc, cert, "https://gov/submit", At)];

        doc.Signatures[0].SubmissionUrl = "https://evil/submit";

        AprVerifier.Verify(doc, doc.Signatures[0]).ContentValid.Should().BeFalse();
    }

    [Fact]
    public void Publisher_SurvivesFilling()
    {
        var doc = Template();
        using var cert = SelfSigned();
        doc.Signatures = [AprSigner.SignTemplate(doc, cert, "https://gov/submit", At)];

        doc.Sections[0].Prompts[0].Response = "Ada Lovelace";

        AprVerifier.Verify(doc, doc.Signatures[0]).ContentValid.Should().BeTrue();
    }

    // ── Filler scope ────────────────────────────────────────────────────────

    [Fact]
    public void Filler_SignsScope_Valid()
    {
        var doc = Template();
        doc.Sections[0].Prompts[0].Response = "Ada";
        using var cert = SelfSigned("Ada Lovelace");
        doc.Signatures = [AprSigner.SignFields(doc, cert, ["name"], At, "sig1")];

        AprVerifier.Verify(doc, doc.Signatures[0]).ContentValid.Should().BeTrue();
    }

    [Fact]
    public void Filler_DetectsTamperWithinScope()
    {
        var doc = Template();
        doc.Sections[0].Prompts[0].Response = "Ada";
        using var cert = SelfSigned();
        doc.Signatures = [AprSigner.SignFields(doc, cert, ["name"], At, "sig1")];

        doc.Sections[0].Prompts[0].Response = "Mallory";

        AprVerifier.Verify(doc, doc.Signatures[0]).ContentValid.Should().BeFalse();
    }

    [Fact]
    public void Filler_ScopeIsolation_OutOfScopeEditIgnored()
    {
        var doc = Template();
        doc.Sections[0].Prompts[0].Response = "Ada";
        using var cert = SelfSigned();
        doc.Signatures = [AprSigner.SignFields(doc, cert, ["name"], At, "sig1")];

        doc.Sections[0].Prompts[2].Response = "edited later"; // notes is out of scope

        AprVerifier.Verify(doc, doc.Signatures[0]).ContentValid.Should().BeTrue();
    }

    [Fact]
    public void MultipleFillers_SignTheirParts_Independently()
    {
        var doc = Template();
        doc.Sections[0].Prompts[0].Response = "Ada";
        doc.Sections[0].Prompts[1].Response = "1815";
        using var k1 = SelfSigned("Ada");
        using var k2 = SelfSigned("Reviewer");
        doc.Signatures =
        [
            AprSigner.SignFields(doc, k1, ["name"], At, "sig-ada"),
            AprSigner.SignFields(doc, k2, ["dob"], At, "sig-rev"),
        ];

        AprVerifier.VerifyAll(doc).Should().OnlyContain(r => r.ContentValid);

        doc.Sections[0].Prompts[0].Response = "Mallory";

        var after = AprVerifier.VerifyAll(doc);
        after.Single(r => r.Id == "sig-ada").ContentValid.Should().BeFalse();
        after.Single(r => r.Id == "sig-rev").ContentValid.Should().BeTrue();
    }

    // ── Trust models ────────────────────────────────────────────────────────

    [Fact]
    public void SelfSigned_NotPinned_IsValidButSelfSignedTrust()
    {
        var doc = Template();
        using var cert = SelfSigned();
        doc.Signatures = [AprSigner.SignTemplate(doc, cert, "https://gov/submit", At)];

        var r = AprVerifier.Verify(doc, doc.Signatures[0]);

        r.ContentValid.Should().BeTrue();
        r.Trust.Should().Be(SignatureTrust.SelfSigned);
    }

    [Fact]
    public void SelfSigned_Pinned_IsTrusted()
    {
        var doc = Template();
        using var cert = SelfSigned();
        doc.Signatures = [AprSigner.SignTemplate(doc, cert, "https://gov/submit", At)];

        var opts = new AprTrustOptions { TrustAnchors = [cert] };
        AprVerifier.Verify(doc, doc.Signatures[0], opts).Trust.Should().Be(SignatureTrust.Trusted);
    }

    [Fact]
    public void CaIssued_TrustedWhenChainsToConfiguredRoot_UntrustedOtherwise()
    {
        var doc = Template();
        using var ca = SignatureCertificates.CreateCertificateAuthority("Town CA", Before, After);
        using var leaf = SignatureCertificates.IssueSigningCertificate(ca, "Clerk", Before, After);
        doc.Signatures = [AprSigner.SignTemplate(doc, leaf, "https://gov/submit", At)];

        // Trusted when the CA is a configured anchor.
        var trusted = AprVerifier.Verify(doc, doc.Signatures[0], new AprTrustOptions { TrustAnchors = [ca] });
        trusted.ContentValid.Should().BeTrue();
        trusted.Trust.Should().Be(SignatureTrust.Trusted);

        // Untrusted when the CA is not trusted (and not a self-signed cert).
        var untrusted = AprVerifier.Verify(doc, doc.Signatures[0], AprTrustOptions.Default);
        untrusted.ContentValid.Should().BeTrue();
        untrusted.Trust.Should().Be(SignatureTrust.Untrusted);
    }

    // ── Persistence ─────────────────────────────────────────────────────────

    [Fact]
    public void Signatures_SurviveJsonRoundTrip()
    {
        var doc = Template();
        doc.Sections[0].Prompts[0].Response = "Ada";
        using var pub = SelfSigned("Publisher");
        using var fill = SelfSigned("Ada");
        doc.Signatures =
        [
            AprSigner.SignTemplate(doc, pub, "https://gov/submit", At),
            AprSigner.SignFields(doc, fill, ["name"], At, "sig1"),
        ];

        var serializer = new AprJsonSerializer();
        var reloaded = serializer.Deserialize(serializer.Serialize(doc));

        AprVerifier.VerifyAll(reloaded).Should().OnlyContain(r => r.ContentValid);
        reloaded.Signatures.Should().HaveCount(2);
    }

    [Fact]
    public void UnsignedDocument_HasNoSignaturesField()
    {
        new AprJsonSerializer().Serialize(Template())
            .Should().NotContain("signatures");
    }
}
