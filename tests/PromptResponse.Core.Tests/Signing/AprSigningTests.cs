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
        doc.Signatures = [SigningTestHelper.SignTemplateWithUrl(doc, cert, "https://bloomfieldct.gov/permit/submit", At)];

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
        doc.Signatures = [SigningTestHelper.SignTemplateWithUrl(doc, cert, "https://x/submit", At)];

        doc.Sections[0].Prompts[0].Label = "Full Name";

        AprVerifier.Verify(doc, doc.Signatures[0]).ContentValid.Should().BeFalse();
    }

    [Fact]
    public void Publisher_BindsSubmissionUrl()
    {
        var doc = Template();
        using var cert = SelfSigned();
        doc.Signatures = [SigningTestHelper.SignTemplateWithUrl(doc, cert, "https://gov/submit", At)];

        // Redirect the field a submitting client actually reads. This is the attack the
        // URL binding exists to stop, and it must break verification.
        //
        // Verification used to recompute the payload from a copy of the URL stored on
        // the signature itself, so this exact edit left the signature reporting VALID
        // while the form submitted somewhere else entirely.
        doc.Metadata.SubmissionUrls = ["https://evil/submit"];

        AprVerifier.Verify(doc, doc.Signatures[0]).ContentValid.Should().BeFalse(
            "the signature binds metadata.submissionUrls, so redirecting one must invalidate the signature");
    }

    [Fact]
    public void Publisher_BindsEverySubmissionUrlAndTheirOrder()
    {
        var doc = Template();
        doc.Metadata.SubmissionUrls = ["mailto:forms@example.org", "https://example.org/submit"];
        using var cert = SelfSigned();
        doc.Signatures = [AprSigner.SignTemplate(doc, cert, At)];

        doc.Metadata.SubmissionUrls.Reverse();

        AprVerifier.Verify(doc, doc.Signatures[0]).ContentValid.Should().BeFalse(
            "a publisher signature binds every delivery choice and their displayed order");
    }

    [Fact]
    public void Publisher_SurvivesFilling()
    {
        var doc = Template();
        using var cert = SelfSigned();
        doc.Signatures = [SigningTestHelper.SignTemplateWithUrl(doc, cert, "https://gov/submit", At)];

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
        doc.Signatures = [SigningTestHelper.SignTemplateWithUrl(doc, cert, "https://gov/submit", At)];

        var r = AprVerifier.Verify(doc, doc.Signatures[0]);

        r.ContentValid.Should().BeTrue();
        r.Trust.Should().Be(SignatureTrust.SelfSigned);
    }

    [Fact]
    public void SelfSigned_Pinned_IsTrusted()
    {
        var doc = Template();
        using var cert = SelfSigned();
        doc.Signatures = [SigningTestHelper.SignTemplateWithUrl(doc, cert, "https://gov/submit", At)];

        var opts = new AprTrustOptions { TrustAnchors = [cert] };
        AprVerifier.Verify(doc, doc.Signatures[0], opts).Trust.Should().Be(SignatureTrust.Trusted);
    }

    [Fact]
    public void CaIssued_TrustedWhenChainsToConfiguredRoot_UntrustedOtherwise()
    {
        var doc = Template();
        using var ca = SignatureCertificates.CreateCertificateAuthority("Town CA", Before, After);
        using var leaf = SignatureCertificates.IssueSigningCertificate(ca, "Clerk", Before, After);
        doc.Signatures = [SigningTestHelper.SignTemplateWithUrl(doc, leaf, "https://gov/submit", At)];

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
            SigningTestHelper.SignTemplateWithUrl(doc, pub, "https://gov/submit", At),
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

    // ── What a filler actually signs (apr-sig-v3) ──────────────────────────

    /// <summary>A filler signs the question, not only the answer.</summary>
    /// <remarks>
    /// Under apr-sig-v2 the payload was "field.id = response" and nothing more, so this
    /// exact edit left the signature verifying. Someone could sign "No" to "have you ever
    /// been convicted of a felony", and the label could afterwards be changed to anything
    /// at all, with their signature still reporting valid over it - putting a person on
    /// record as having answered a question they never saw.
    ///
    /// The same family of bug as the submissionUrl one above: a signature verifying over
    /// something other than what was actually presented.
    /// </remarks>
    [Fact]
    public void Filler_DetectsTheQuestionBeingRewrittenAfterSigning()
    {
        var doc = Template();
        doc.Sections[0].Prompts[0].Response = "No";
        using var cert = SelfSigned("Ada Lovelace");
        doc.Signatures = [AprSigner.SignFields(doc, cert, ["name"], At, "sig1")];

        AprVerifier.Verify(doc, doc.Signatures[0]).ContentValid.Should().BeTrue("baseline");

        // The answer is untouched. Only the question changes.
        doc.Sections[0].Prompts[0].Label = "Do you enjoy long walks?";

        AprVerifier.Verify(doc, doc.Signatures[0]).ContentValid.Should().BeFalse(
            "a filler signature binds the question as it was presented, so swapping the " +
            "question must invalidate it");
    }

    [Fact]
    public void Filler_DetectsTheOfferedOptionsBeingChangedAfterSigning()
    {
        var doc = Template();
        doc.Sections[0].Prompts[0].Response = "Sales";
        doc.Sections[0].Prompts[0].Hints.SuggestedValues = ["Sales", "Finance"];
        using var cert = SelfSigned();
        doc.Signatures = [AprSigner.SignFields(doc, cert, ["name"], At, "sig1")];

        doc.Sections[0].Prompts[0].Hints.SuggestedValues = ["Sales", "Fraud"];

        AprVerifier.Verify(doc, doc.Signatures[0]).ContentValid.Should().BeFalse(
            "the shortlist someone chose from is part of what they saw");
    }

    [Fact]
    public void Filler_DetectsTheDeclaredTypeBeingChangedAfterSigning()
    {
        var doc = Template();
        doc.Sections[0].Prompts[0].Response = "10";
        doc.Sections[0].Prompts[0].Hints.ExpectedDataType = "number";
        using var cert = SelfSigned();
        doc.Signatures = [AprSigner.SignFields(doc, cert, ["name"], At, "sig1")];

        doc.Sections[0].Prompts[0].Hints.ExpectedDataType = "currency";

        AprVerifier.Verify(doc, doc.Signatures[0]).ContentValid.Should().BeFalse(
            "\"10\" means something different when the field is money");
    }

    /// <summary>Binding the question must not cost scope isolation.</summary>
    /// <remarks>
    /// A filler signs their part. Someone else editing an unrelated question afterwards -
    /// which is the normal course of a multi-party form - must not invalidate them, or
    /// nobody could sign until everybody had finished.
    /// </remarks>
    [Fact]
    public void Filler_StillIgnoresQuestionsOutsideTheirScope()
    {
        var doc = Template();
        doc.Sections[0].Prompts[0].Response = "Ada";
        using var cert = SelfSigned();
        doc.Signatures = [AprSigner.SignFields(doc, cert, ["name"], At, "sig1")];

        doc.Sections[0].Prompts[2].Label = "Rewritten later by someone else";

        AprVerifier.Verify(doc, doc.Signatures[0]).ContentValid.Should().BeTrue(
            "notes is outside this signature's scope, so its wording is none of its business");
    }

    /// <summary>A publisher signature binds the bounds it published.</summary>
    /// <remarks>
    /// The bounds family was added after the list of signed hints was written, so on
    /// apr-sig-v2 a signed template's slider could be re-ranged without breaking the
    /// signature.
    /// </remarks>
    [Fact]
    public void Publisher_DetectsBoundsBeingChangedAfterSigning()
    {
        var doc = Template();
        doc.Sections[0].Prompts[1].Hints.ExpectedDataType = "range";
        doc.Sections[0].Prompts[1].Hints.Min = "0";
        doc.Sections[0].Prompts[1].Hints.Max = "10";
        using var cert = SelfSigned();
        doc.Signatures = [SigningTestHelper.SignTemplateWithUrl(doc, cert, "https://gov/submit", At)];

        doc.Sections[0].Prompts[1].Hints.Max = "1000";

        AprVerifier.Verify(doc, doc.Signatures[0]).ContentValid.Should().BeFalse(
            "the range a publisher offered is part of the form they published");
    }

    /// <summary>A publisher signature binds who each part was meant for.</summary>
    [Fact]
    public void Publisher_DetectsARoleBeingChangedAfterSigning()
    {
        var doc = Template();
        doc.Sections[0].Role = "patient";
        using var cert = SelfSigned();
        doc.Signatures = [SigningTestHelper.SignTemplateWithUrl(doc, cert, "https://gov/submit", At)];

        doc.Sections[0].Role = "office";

        AprVerifier.Verify(doc, doc.Signatures[0]).ContentValid.Should().BeFalse(
            "reassigning a section to a different party changes the form that was published");
    }
}
