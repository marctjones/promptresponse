using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Signing;
using Xunit;

namespace PromptResponse.Core.Tests.Signing;

/// <summary>
/// Verifies the apr-sig-v1 signing protocol: publisher signs the template
/// (binding the submission URL), fillers sign scoped responses, tampering is
/// detected, scopes are isolated, and signatures survive JSON round-trips.
/// </summary>
public class AprSigningTests
{
    private static readonly DateTime At = new(2026, 6, 8, 12, 0, 0, DateTimeKind.Utc);

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

    // ── Publisher signs the template ────────────────────────────────────────

    [Fact]
    public void Publisher_SignsTemplate_Verifies()
    {
        var doc = Template();
        using var key = SignatureKeys.Generate();
        doc.Signatures = [AprSigner.SignTemplate(doc, key, "Town of Bloomfield", "clerk@bloomfieldct.gov",
            "https://bloomfieldct.gov/forms/permit/submit", At)];

        var result = AprVerifier.VerifyAll(doc).Single();

        result.IsValid.Should().BeTrue();
        result.Role.Should().Be(SignatureRole.Publisher);
        result.SignerName.Should().Be("Town of Bloomfield");
    }

    [Fact]
    public void Publisher_DetectsFormDefinitionTampering()
    {
        var doc = Template();
        using var key = SignatureKeys.Generate();
        doc.Signatures = [AprSigner.SignTemplate(doc, key, "Pub", null, "https://x/submit", At)];

        doc.Sections[0].Prompts[0].Label = "Full Name"; // alter the form definition

        AprVerifier.Verify(doc, doc.Signatures[0]).IsValid
            .Should().BeFalse("changing a label changes the signed form definition");
    }

    [Fact]
    public void Publisher_Signature_BindsSubmissionUrl()
    {
        var doc = Template();
        using var key = SignatureKeys.Generate();
        doc.Signatures = [AprSigner.SignTemplate(doc, key, "Pub", null, "https://gov/submit", At)];

        doc.Signatures[0].SubmissionUrl = "https://evil/submit"; // redirect attempt

        AprVerifier.Verify(doc, doc.Signatures[0]).IsValid
            .Should().BeFalse("the submission URL is bound into the publisher signature");
    }

    [Fact]
    public void Publisher_Signature_SurvivesFilling()
    {
        var doc = Template();
        using var key = SignatureKeys.Generate();
        doc.Signatures = [AprSigner.SignTemplate(doc, key, "Pub", null, "https://gov/submit", At)];

        doc.Sections[0].Prompts[0].Response = "Ada Lovelace"; // a filler enters a response

        AprVerifier.Verify(doc, doc.Signatures[0]).IsValid
            .Should().BeTrue("responses are not part of the signed form definition");
    }

    // ── Filler signs scoped responses ───────────────────────────────────────

    [Fact]
    public void Filler_SignsScope_Verifies()
    {
        var doc = Template();
        doc.Sections[0].Prompts[0].Response = "Ada";
        doc.Sections[0].Prompts[1].Response = "1815-12-10";
        using var key = SignatureKeys.Generate();
        doc.Signatures = [AprSigner.SignFields(doc, key, "Ada", null, ["name", "dob"], At, "sig1")];

        AprVerifier.Verify(doc, doc.Signatures[0]).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Filler_DetectsTamperWithinScope()
    {
        var doc = Template();
        doc.Sections[0].Prompts[0].Response = "Ada";
        using var key = SignatureKeys.Generate();
        doc.Signatures = [AprSigner.SignFields(doc, key, "Ada", null, ["name"], At, "sig1")];

        doc.Sections[0].Prompts[0].Response = "Mallory"; // alter a covered response

        AprVerifier.Verify(doc, doc.Signatures[0]).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Filler_ScopeIsolation_OutOfScopeEditDoesNotInvalidate()
    {
        var doc = Template();
        doc.Sections[0].Prompts[0].Response = "Ada";
        using var key = SignatureKeys.Generate();
        doc.Signatures = [AprSigner.SignFields(doc, key, "Ada", null, ["name"], At, "sig1")];

        doc.Sections[0].Prompts[2].Response = "edited later"; // "notes" is out of scope

        AprVerifier.Verify(doc, doc.Signatures[0]).IsValid
            .Should().BeTrue("editing a field outside the signature's scope must not invalidate it");
    }

    [Fact]
    public void MultipleFillers_EachSignTheirParts_Independently()
    {
        var doc = Template();
        doc.Sections[0].Prompts[0].Response = "Ada";   // signed by filler 1
        doc.Sections[0].Prompts[1].Response = "1815";  // signed by filler 2
        using var k1 = SignatureKeys.Generate();
        using var k2 = SignatureKeys.Generate();
        doc.Signatures =
        [
            AprSigner.SignFields(doc, k1, "Ada", null, ["name"], At, "sig-ada"),
            AprSigner.SignFields(doc, k2, "Reviewer", null, ["dob"], At, "sig-rev"),
        ];

        AprVerifier.VerifyAll(doc).Should().OnlyContain(r => r.IsValid);

        doc.Sections[0].Prompts[0].Response = "Mallory"; // tamper only filler 1's field

        var after = AprVerifier.VerifyAll(doc);
        after.Single(r => r.Id == "sig-ada").IsValid.Should().BeFalse();
        after.Single(r => r.Id == "sig-rev").IsValid.Should().BeTrue("the reviewer's scope is untouched");
    }

    // ── Persistence ─────────────────────────────────────────────────────────

    [Fact]
    public void Signatures_SurviveJsonRoundTrip()
    {
        var doc = Template();
        doc.Sections[0].Prompts[0].Response = "Ada";
        using var pubKey = SignatureKeys.Generate();
        using var fillKey = SignatureKeys.Generate();
        doc.Signatures =
        [
            AprSigner.SignTemplate(doc, pubKey, "Pub", null, "https://gov/submit", At),
            AprSigner.SignFields(doc, fillKey, "Ada", null, ["name"], At, "sig1"),
        ];

        var serializer = new AprJsonSerializer();
        var reloaded = serializer.Deserialize(serializer.Serialize(doc));

        AprVerifier.VerifyAll(reloaded).Should().OnlyContain(r => r.IsValid,
            "signatures must verify after a serialize/deserialize cycle");
        reloaded.Signatures.Should().HaveCount(2);
    }

    [Fact]
    public void UnsignedDocument_HasNoSignaturesField()
    {
        var json = new AprJsonSerializer().Serialize(Template());
        json.Should().NotContain("signatures", "an unsigned document should not carry a signatures field");
    }
}
