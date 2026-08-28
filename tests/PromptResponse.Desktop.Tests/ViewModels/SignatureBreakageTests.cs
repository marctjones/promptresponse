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
/// Telling somebody their edit broke a signature — and never quietly fixing it for them.
/// </summary>
/// <remarks>
/// A broken signature is evidence: somebody signed this and the document changed
/// afterwards. An editor that discarded it to tidy up would convert that into "nobody
/// ever signed this", which is strictly less informative and would make this application
/// the easiest tampering tool anyone could reach for.
/// </remarks>
public class SignatureBreakageTests
{
    private sealed class Probe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static readonly DateTime At = new(2026, 6, 8, 12, 0, 0, DateTimeKind.Utc);

    private static AprDocument Signed(out X509Certificate2 cert)
    {
        var document = new AprDocument
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
                        new Prompt { Id = "amount", Label = "Amount", Response = "100" },
                        new Prompt { Id = "notes", Label = "Notes", Response = "none" },
                    ],
                },
            ],
        };
        cert = SignatureCertificates.CreateSelfSigned(
            "Ada Lovelace", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(2));
        document.Signatures = [AprSigner.SignFields(document, cert, ["amount"], At, "sig1")];
        return document;
    }

    private static (MainShellViewModel Shell, IDialogService Dialogs, DocumentSessionService Session)
        ShellOver(AprDocument document)
    {
        var session = new DocumentSessionService();
        var dialogs = Substitute.For<IDialogService>();
        var profile = new ProfileService(new Probe(), applyAffordanceDefaults: false);
        var shell = new MainShellViewModel(Substitute.For<IFileService>(), dialogs,
            session, profile, new PromptViewModelFactory(profile));
        session.Set(document, null);
        return (shell, dialogs, session);
    }

    private static PromptViewModelBase Field(MainShellViewModel shell, string id) =>
        shell.PromptViewModels.Single(p => p.Id == id);

    // ── The notice ───────────────────────────────────────────────────────────

    [Fact]
    public void BreakingASignature_SaysSoAtTheMomentItHappens()
    {
        var document = Signed(out var certificate);
        using (certificate)
        {
            var (shell, _, _) = ShellOver(document);
            shell.HasSignatureBreakageNotice.Should().BeFalse("nothing has happened yet");

            Field(shell, "amount").Response = "250";

            shell.HasSignatureBreakageNotice.Should().BeTrue();
            shell.SignatureBreakageNotice.Should().Contain("Ada Lovelace")
                .And.Contain("no longer verifies");
            shell.SignatureBreakageNotice.Should().Contain("still saves",
                "somebody must not be left thinking the document is now unusable; it is " +
                "still valid and a signature never withholds data (specification 9.5)");
        }
    }

    [Fact]
    public void TheEditIsNeverBlocked()
    {
        var document = Signed(out var certificate);
        using (certificate)
        {
            var (shell, _, _) = ShellOver(document);
            var amount = Field(shell, "amount");

            amount.Response = "250";

            amount.Response.Should().Be("250", "any string is a valid response; the answer " +
                "to a person needing to correct a signed field is not to stop them");
            amount.IsInputEnabled.Should().BeTrue();
        }
    }

    [Fact]
    public void EditingAnUnsignedField_SaysNothing()
    {
        var document = Signed(out var certificate);
        using (certificate)
        {
            var (shell, _, _) = ShellOver(document);

            Field(shell, "notes").Response = "changed later";

            shell.HasSignatureBreakageNotice.Should().BeFalse(
                "notes was never in Ada's scope, so nothing of hers broke");
        }
    }

    [Fact]
    public void TheNoticeAppearsOnce_NotOnEveryKeystroke()
    {
        var document = Signed(out var certificate);
        using (certificate)
        {
            var (shell, _, _) = ShellOver(document);
            var amount = Field(shell, "amount");

            amount.Response = "250";
            shell.DismissSignatureNoticeCommand.Execute(null);
            amount.Response = "251";
            amount.Response = "252";

            shell.HasSignatureBreakageNotice.Should().BeFalse(
                "a message that reappears on every keystroke is one people learn to " +
                "dismiss without reading");
        }
    }

    [Fact]
    public void PuttingTheValueBack_RestoresTheSignatureAndClearsTheNotice()
    {
        var document = Signed(out var certificate);
        using (certificate)
        {
            var (shell, _, _) = ShellOver(document);
            Field(shell, "amount").Response = "250";

            shell.RestoreSignedValuesCommand.Execute(null);

            shell.HasSignatureBreakageNotice.Should().BeFalse();
            Field(shell, "amount").SignatureState.Should().Be(FieldSignatureState.Signed,
                "putting the value back makes the signature verify again");
        }
    }

    // ── Removal: deliberate, never automatic ─────────────────────────────────

    [Fact]
    public async Task BreakingASignature_NeverRemovesIt()
    {
        var document = Signed(out var certificate);
        using (certificate)
        {
            var (shell, _, _) = ShellOver(document);

            Field(shell, "amount").Response = "250";

            document.Signatures.Should().HaveCount(1,
                "a broken signature is evidence that somebody signed this and it changed " +
                "afterwards. Discarding it would turn that into \"nobody ever signed this\"");
            shell.Signatures.Should().ContainSingle(s => !s.ContentValid);
            await Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RemovingASignature_AsksFirst()
    {
        var document = Signed(out var certificate);
        using (certificate)
        {
            var (shell, dialogs, _) = ShellOver(document);
            dialogs.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

            await shell.RemoveSignatureCommand.ExecuteAsync("sig1");

            document.Signatures.Should().HaveCount(1, "the answer was no");
            await dialogs.Received(1).ShowConfirmationAsync(Arg.Any<string>(),
                Arg.Is<string>(m => m.Contains("evidence")));
        }
    }

    [Fact]
    public async Task RemovingASignature_WhenConfirmed_TakesItOut()
    {
        var document = Signed(out var certificate);
        using (certificate)
        {
            var (shell, dialogs, session) = ShellOver(document);
            dialogs.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

            await shell.RemoveSignatureCommand.ExecuteAsync("sig1");

            document.Signatures.Should().BeNull("the last one leaves no empty array behind");
            shell.HasSignatures.Should().BeFalse();
            session.IsDirty.Should().BeTrue("removing a signature changes the document");
        }
    }

    [Fact]
    public async Task RemovingASignatureThatIsNotThere_DoesNothing()
    {
        var document = Signed(out var certificate);
        using (certificate)
        {
            var (shell, dialogs, _) = ShellOver(document);
            dialogs.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

            await shell.RemoveSignatureCommand.ExecuteAsync("no-such-signature");

            document.Signatures.Should().HaveCount(1);
            await dialogs.DidNotReceive().ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>());
        }
    }
}
