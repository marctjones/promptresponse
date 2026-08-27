using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Core.Models;
using PromptResponse.Core.Signing;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>
/// Is <em>this</em> field signed, by whom, and does it still hold?
/// </summary>
/// <remarks>
/// The sidebar can say "Ada's signature is valid". On a hundred-field form with one
/// signature over twelve of them, that does not tell the person looking at a field
/// whether it is one of the twelve.
/// </remarks>
public class SignatureCoverageDisplayTests
{
    private sealed class Probe(bool colorCues, LiveRegionVerbosity verbosity) : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
        public bool ColorCues { get; } = colorCues;
        public LiveRegionVerbosity Verbosity { get; } = verbosity;
    }

    private static readonly DateTime At = new(2026, 6, 8, 12, 0, 0, DateTimeKind.Utc);

    private static AprDocument Claim() => new()
    {
        DocumentType = DocumentType.FilledForm,
        Metadata = new Metadata { Title = "Claim", TemplateId = "c", TemplateVersion = "1.0" },
        Sections =
        [
            new Section
            {
                Id = "s", Title = "Claim",
                Prompts =
                [
                    new Prompt { Id = "name", Label = "Name", Response = "Ada" },
                    new Prompt { Id = "amount", Label = "Amount", Response = "100" },
                    new Prompt { Id = "notes", Label = "Notes", Response = "none" },
                ],
            },
        ],
    };

    private static X509Certificate2 Cert(string cn) => SignatureCertificates.CreateSelfSigned(
        cn, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(2));

    private static MainShellViewModel ShellOver(AprDocument document)
    {
        var session = new DocumentSessionService();
        var profile = new ProfileService(new Probe(true, LiveRegionVerbosity.Normal),
            applyAffordanceDefaults: false);
        var shell = new MainShellViewModel(Substitute.For<IFileService>(),
            Substitute.For<IDialogService>(), session, profile, new PromptViewModelFactory(profile));
        session.Set(document, null);
        return shell;
    }

    private static PromptViewModelBase Field(MainShellViewModel shell, string id) =>
        shell.PromptViewModels.Single(p => p.Id == id);

    [Fact]
    public void AnUnsignedField_ShowsNothing()
    {
        var shell = ShellOver(Claim());

        var field = Field(shell, "name");
        field.SignatureState.Should().Be(FieldSignatureState.Unsigned);
        field.ShowSignatureMark.Should().BeFalse(
            "most documents are never signed; marking every field \"unsigned\" would make " +
            "the ordinary case look like a warning and teach people to ignore the marks");
        field.SignatureLabel.Should().BeNull();
    }

    [Fact]
    public void AFieldInsideAValidSignaturesScope_SaysWhoSignedIt()
    {
        var doc = Claim();
        using var cert = Cert("Ada Lovelace");
        doc.Signatures = [AprSigner.SignFields(doc, cert, ["name", "amount"], At, "sig1")];

        var shell = ShellOver(doc);

        Field(shell, "name").SignatureState.Should().Be(FieldSignatureState.Signed);
        Field(shell, "name").SignatureLabel.Should().Be("Signed by Ada Lovelace");
        Field(shell, "notes").ShowSignatureMark.Should().BeFalse(
            "notes is outside the scope, so nobody has attested to it");
    }

    [Fact]
    public void TwoSignersOnOneField_AreCountedRatherThanListed()
    {
        var doc = Claim();
        using var first = Cert("Ada Lovelace");
        using var second = Cert("Grace Hopper");
        doc.Signatures =
        [
            AprSigner.SignFields(doc, first, ["amount"], At, "sig1"),
            AprSigner.SignFields(doc, second, ["amount"], At, "sig2"),
        ];

        var shell = ShellOver(doc);

        Field(shell, "amount").SignatureLabel.Should().Be("Signed by 2 people",
            "a field beside a field cannot hold a roster; the panel has the names");
    }

    /// <summary>The state that actually warrants attention.</summary>
    [Fact]
    public void EditingASignedField_TurnsItsMarkToBroken_Immediately()
    {
        var doc = Claim();
        using var cert = Cert("Ada Lovelace");
        doc.Signatures = [AprSigner.SignFields(doc, cert, ["amount"], At, "sig1")];

        var shell = ShellOver(doc);
        var amount = Field(shell, "amount");
        amount.SignatureState.Should().Be(FieldSignatureState.Signed, "baseline");

        amount.Response = "250";

        amount.SignatureState.Should().Be(FieldSignatureState.Broken,
            "typing into a signed field is exactly what invalidates a signature, so the " +
            "mark must change on that keystroke rather than when somebody presses Refresh");
        amount.SignatureLabel.Should().Be("Signature broken");
        amount.IsInputEnabled.Should().BeTrue(
            "and a broken signature still never blocks editing (specification 9.5)");
    }

    [Fact]
    public void EditingAnUnsignedField_LeavesOtherFieldsMarksAlone()
    {
        var doc = Claim();
        using var cert = Cert("Ada Lovelace");
        doc.Signatures = [AprSigner.SignFields(doc, cert, ["amount"], At, "sig1")];

        var shell = ShellOver(doc);
        Field(shell, "notes").Response = "edited later by someone else";

        Field(shell, "amount").SignatureState.Should().Be(FieldSignatureState.Signed,
            "scope isolation: notes was never Ada's to sign");
    }

    // ── Appropriate to the profile ───────────────────────────────────────────

    [Fact]
    public void TheMarkIsAlwaysReadableAsText_NotOnlyAsColour()
    {
        var doc = Claim();
        using var cert = Cert("Ada Lovelace");
        doc.Signatures = [AprSigner.SignFields(doc, cert, ["amount"], At, "sig1")];

        Field(ShellOver(doc), "amount").SignatureLabel.Should().NotBeNullOrWhiteSpace(
            "colour is an accompaniment, never the message: a profile may have colour cues " +
            "off, a reader may be colour-blind, and a printout has no state at all");
    }

    [Fact]
    public void WhoSignedIt_ReachesAssistiveTechnology()
    {
        var doc = Claim();
        using var cert = Cert("Ada Lovelace");
        doc.Signatures = [AprSigner.SignFields(doc, cert, ["amount"], At, "sig1")];

        var shell = ShellOver(doc);
        Field(shell, "amount").SignatureAnnouncement.Should().Be(
            "Signed by Ada Lovelace. Editing it will break their signature.",
            "a screen-reader user cannot glance at the sidebar for the rest of the story");

        Field(shell, "amount").Response = "999";
        Field(shell, "amount").SignatureAnnouncement.Should().Contain("no longer verifies")
            .And.Contain("still edit",
                "the broken state must say both what happened and that they are not stuck");
    }
}
