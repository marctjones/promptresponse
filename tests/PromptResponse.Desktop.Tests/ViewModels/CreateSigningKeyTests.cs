using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Signing;
using PromptResponse.Desktop.ViewModels;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// Making a self-signed signing key from the GUI.
/// </summary>
/// <remarks>
/// Creation only. There is no keystore, no rotation, no revocation and no renewal here -
/// that is where key management gets hard, and the platform already does it. The point of
/// this is that somebody can try signing without first learning openssl.
/// </remarks>
public class CreateSigningKeyTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "apr-key-" + Guid.NewGuid().ToString("N"));

    public CreateSigningKeyTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    private string Path_(string name) => Path.Combine(_dir, name);

    [Fact]
    public void ItWritesAKeyThatCanActuallySignADocument()
    {
        var vm = new CreateSigningKeyViewModel { SignerName = "Town of Bloomfield", Password = "hunter2" };

        var created = vm.Create(Path_("town.pfx"));

        File.Exists(created.PrivateKeyPath).Should().BeTrue();
        created.Subject.Should().Contain("Town of Bloomfield");

        // The real test of a signing key is whether it signs.
        var document = new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata { Title = "Permit", TemplateId = "p", TemplateVersion = "1.0" },
            Sections =
            [
                new Section { Id = "s", Title = "S",
                    Prompts = [new Prompt { Id = "name", Label = "Name", Response = "Ada" }] },
            ],
        };
        using var loaded = SignatureCertificates.LoadPfx(created.PrivateKeyPath, "hunter2");
        document.Signatures =
            [AprSigner.SignFields(document, loaded, ["name"], DateTime.UtcNow, "sig1")];

        AprVerifier.Verify(document, document.Signatures[0]).ContentValid.Should().BeTrue(
            "a key this dialog produced must be usable for the thing the dialog exists for");
    }

    [Fact]
    public void ThePublicCertificateIsWrittenBeside_SoOthersCanVerify()
    {
        var created = new CreateSigningKeyViewModel { SignerName = "Ada Lovelace" }
            .Create(Path_("ada.pfx"));

        created.PublicCertificatePath.Should().EndWith(".cer");
        File.Exists(created.PublicCertificatePath!).Should().BeTrue(
            "without it a self-signed signature can only ever report \"self-signed\", never " +
            "\"trusted\" — sharing it is what makes the key useful to anybody else");

        using var shared = SignatureCertificates.LoadCertificate(created.PublicCertificatePath!);
        shared.HasPrivateKey.Should().BeFalse(
            "the file you hand out must not contain your private key");
        SignatureCertificates.Sha256Thumbprint(shared).Should().Be(created.Thumbprint,
            "the thumbprint shown is the one a recipient pins");
    }

    [Fact]
    public void PinningTheSharedCertificate_MakesTheSignatureTrustedRatherThanMerelySelfSigned()
    {
        var created = new CreateSigningKeyViewModel { SignerName = "Town of Bloomfield" }
            .Create(Path_("town.pfx"));

        var document = new AprDocument
        {
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata { Title = "Permit", TemplateId = "p", TemplateVersion = "1.0" },
            Sections =
            [
                new Section { Id = "s", Title = "S",
                    Prompts = [new Prompt { Id = "name", Label = "Name", Response = "Ada" }] },
            ],
        };
        using var key = SignatureCertificates.LoadPfx(created.PrivateKeyPath);
        document.Signatures = [AprSigner.SignFields(document, key, ["name"], DateTime.UtcNow, "s1")];

        AprVerifier.Verify(document, document.Signatures[0]).Trust
            .Should().Be(SignatureTrust.SelfSigned, "nobody has chosen to trust it yet");

        using var pinned = SignatureCertificates.LoadCertificate(created.PublicCertificatePath!);
        AprVerifier.Verify(document, document.Signatures[0],
                new AprTrustOptions { TrustAnchors = [pinned] })
            .Trust.Should().Be(SignatureTrust.Trusted,
                "which is exactly what sharing the .cer is for");
    }

    [Fact]
    public void OptingOutOfThePublicCertificate_WritesOnlyTheKey()
    {
        var created = new CreateSigningKeyViewModel
        {
            SignerName = "Ada Lovelace",
            AlsoWritePublicCertificate = false,
        }.Create(Path_("private-only.pfx"));

        created.PublicCertificatePath.Should().BeNull();
        File.Exists(Path.ChangeExtension(created.PrivateKeyPath, ".cer")).Should().BeFalse();
    }

    [Fact]
    public void AnUnprotectedKeyIsAllowed_ButThePasswordIsHonouredWhenGiven()
    {
        var open = new CreateSigningKeyViewModel { SignerName = "No Password" }.Create(Path_("open.pfx"));
        var act = () => SignatureCertificates.LoadPfx(open.PrivateKeyPath);
        act.Should().NotThrow("a blank password means no password, not a broken file");

        var locked = new CreateSigningKeyViewModel { SignerName = "Locked", Password = "s3cret" }
            .Create(Path_("locked.pfx"));
        var wrong = () => SignatureCertificates.LoadPfx(locked.PrivateKeyPath, "not-the-password");
        wrong.Should().Throw<Exception>("the password must actually protect the key");
    }

    // ── What the dialog will and will not let you do ─────────────────────────

    [Theory]
    [InlineData("", 3, false)]
    [InlineData("   ", 3, false)]
    [InlineData("Ada", 0, false)]
    [InlineData("Ada", 31, false)]
    [InlineData("Ada", 1, true)]
    [InlineData("Ada", 30, true)]
    public void CreationRequiresANameAndASensibleLifetime(string name, int years, bool expected) =>
        new CreateSigningKeyViewModel { SignerName = name, ValidYears = years }
            .CanCreate.Should().Be(expected);

    [Theory]
    [InlineData("Town of Bloomfield", "town-of-bloomfield.pfx")]
    [InlineData("Ada Lovelace", "ada-lovelace.pfx")]
    [InlineData("R&D / Legal", "r-d-legal.pfx")]
    [InlineData("!!!", "signing-key.pfx")]
    public void TheSuggestedFileNameIsSafeToPutOnAnyFilesystem(string name, string expected) =>
        new CreateSigningKeyViewModel { SignerName = name }.SuggestedFileName.Should().Be(expected);

    [Fact]
    public void TheKeyExpires_AndSignaturesMadeWithItKeepVerifyingAfterwards()
    {
        var created = new CreateSigningKeyViewModel { SignerName = "Ada", ValidYears = 2 }
            .Create(Path_("expiring.pfx"));

        created.Expires.Should().BeAfter(DateTime.UtcNow.AddYears(1))
            .And.BeBefore(DateTime.UtcNow.AddYears(3),
                "expiry is about how long the key may be used; specification 9.4 reports an " +
                "expired signer as a trust note and never as invalid content");
    }
}
