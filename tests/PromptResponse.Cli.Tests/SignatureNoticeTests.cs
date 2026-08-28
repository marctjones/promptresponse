using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using PromptResponse.Cli;
using PromptResponse.Core.Models;
using PromptResponse.Core.Signing;
using Xunit;

namespace PromptResponse.Cli.Tests;

/// <summary>
/// Anyone opening a document is told when a signature has broken.
/// </summary>
/// <remarks>
/// Before this, only `verify` mentioned signatures — so somebody had to already suspect
/// a problem in order to be told about one. `info`, `validate`, `stats` and `review` were
/// all silent about a document that had been altered after somebody signed it.
/// </remarks>
public class SignatureNoticeTests
{
    private static readonly DateTime At = new(2026, 6, 8, 12, 0, 0, DateTimeKind.Utc);

    private static AprDocument Form() => new()
    {
        DocumentType = DocumentType.FilledForm,
        Metadata = new Metadata { Title = "Permit", TemplateId = "p", TemplateVersion = "1.0" },
        Sections =
        [
            new Section
            {
                Id = "s", Title = "Applicant",
                Prompts = [new Prompt { Id = "name", Label = "Name", Response = "Ada" }],
            },
        ],
    };

    private static X509Certificate2 Cert() => SignatureCertificates.CreateSelfSigned(
        "Ada Lovelace", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(2));

    private static (bool Broken, string Output) Notice(AprDocument document)
    {
        var writer = new StringWriter();
        var broken = SignatureNotice.Write(document, writer);
        return (broken, writer.ToString());
    }

    [Fact]
    public void AnUnsignedDocument_SaysNothingAtAll()
    {
        var (broken, output) = Notice(Form());

        broken.Should().BeFalse();
        output.Should().BeEmpty(
            "signing is optional and most documents are never signed. Treating unsigned " +
            "as a warning would make the common case look alarming and teach people to " +
            "dismiss the message, which disarms it for the case that matters");
    }

    [Fact]
    public void AnIntactSignature_IsConfirmedQuietly()
    {
        var document = Form();
        using var cert = Cert();
        document.Signatures = [AprSigner.SignFields(document, cert, ["name"], At, "sig1")];

        var (broken, output) = Notice(document);

        broken.Should().BeFalse();
        output.Should().Contain("all verify").And.Contain("Ada Lovelace");
        output.Should().Contain("self-signed, not pinned",
            "trust in words, because the enum name alone tells a reader nothing");
    }

    [Fact]
    public void ABrokenSignature_IsReportedLoudly_AndSaysWhatItMeans()
    {
        var document = Form();
        using var cert = Cert();
        document.Signatures = [AprSigner.SignFields(document, cert, ["name"], At, "sig1")];
        document.Sections[0].Prompts[0].Response = "Mallory";

        var (broken, output) = Notice(document);

        broken.Should().BeTrue();
        output.Should().Contain("BROKEN").And.Contain("no longer verify");
        output.Should().Contain("The data is still",
            "the message must say the data is readable, or somebody will think the file " +
            "is unusable when the format guarantees the opposite (specification 9.5)");
    }

    [Fact]
    public void OneBrokenAmongSeveral_NamesWhichOne()
    {
        var document = Form();
        document.Sections[0].Prompts.Add(new Prompt { Id = "dob", Label = "DOB", Response = "1815" });
        using var first = Cert();
        using var second = SignatureCertificates.CreateSelfSigned(
            "Grace Hopper", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(2));
        document.Signatures =
        [
            AprSigner.SignFields(document, first, ["name"], At, "sig1"),
            AprSigner.SignFields(document, second, ["dob"], At, "sig2"),
        ];

        document.Sections[0].Prompts[0].Response = "Mallory";   // breaks only Ada's

        var (broken, output) = Notice(document);

        broken.Should().BeTrue();
        output.Should().Contain("1 of 2 no longer verify");
        output.Should().Contain("BROKEN").And.Contain("Ada Lovelace");
        output.Should().Contain("ok").And.Contain("Grace Hopper",
            "scope isolation means Grace's signature still stands, and saying so is the " +
            "difference between a useful report and an alarm");
    }
}
