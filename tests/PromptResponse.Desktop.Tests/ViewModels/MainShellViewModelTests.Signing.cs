using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Core.Models;
using PromptResponse.Core.Signing;
using PromptResponse.Desktop.Services;
using Xunit;

namespace PromptResponse.Desktop.Tests.ViewModels;

/// <summary>Signature review and signing workflows for the desktop shell.</summary>
public partial class MainShellViewModelTests
{
    [Fact]
    public void LoadingSignedDocument_PopulatesSignaturesPanel()
    {
        var doc = MakeTemplate(); using var cert = SignatureCertificates.CreateSelfSigned("Town of Bloomfield", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1)); doc.Signatures = [AprSigner.SignTemplate(doc, cert, DateTime.UtcNow)];
        var session = new DocumentSessionService(); var shell = CreateShell(session: session); session.Set(doc, null);
        shell.HasSignatures.Should().BeTrue(); shell.Signatures.Should().ContainSingle(); shell.Signatures[0].Role.Should().Be("Publisher"); shell.Signatures[0].Signer.Should().Be("Town of Bloomfield"); shell.SignatureSummary.Should().Contain("all verify");
    }

    [Fact]
    public void LoadingUnsignedDocument_HasNoSignatures()
    {
        var session = new DocumentSessionService(); var shell = CreateShell(session: session); session.Set(MakeTemplate(), null);
        shell.HasSignatures.Should().BeFalse(); shell.SignatureSummary.Should().Be("Not signed");
    }

    [Fact]
    public void RefreshSignatures_AfterTamperingASignedField_ReportsInvalid()
    {
        var doc = MakeTemplate(); doc.Sections[0].Prompts[0].Response = "Ada"; using var cert = SignatureCertificates.CreateSelfSigned("Ada", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1)); doc.Signatures = [AprSigner.SignFields(doc, cert, ["p1"], DateTime.UtcNow, "f1")];
        var session = new DocumentSessionService(); var shell = CreateShell(session: session); session.Set(doc, null); shell.Signatures[0].ContentValid.Should().BeTrue(); doc.Sections[0].Prompts[0].Response = "Mallory"; shell.RefreshSignatures();
        shell.Signatures[0].ContentValid.Should().BeFalse(); shell.SignatureSummary.Should().Contain("INVALID");
    }

    private static string MakeTempPfx(string cn = "Town of Bloomfield")
    {
        using var cert = SignatureCertificates.CreateSelfSigned(cn, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var path = Path.Combine(Path.GetTempPath(), $"cert_{Guid.NewGuid():N}.pfx"); File.WriteAllBytes(path, cert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pkcs12)); return path;
    }

    [Fact]
    public async Task SignAsPublisher_AddsPublisherSignature_BindsUrl_AndMarksDirty()
    {
        var pfx = MakeTempPfx(); var fs = Substitute.For<IFileService>(); var dlg = Substitute.For<IDialogService>(); fs.PickCertificateAsync().Returns(pfx); dlg.ShowInputAsync("Certificate password", Arg.Any<string>(), Arg.Any<string>(), true).Returns(""); dlg.ShowInputAsync("Submission URLs", Arg.Any<string>(), Arg.Any<string>(), false).Returns("https://gov/submit");
        var session = new DocumentSessionService(); var shell = CreateShell(fs, dialogService: dlg, session: session); session.Set(MakeTemplate(), null);
        try { await shell.SignAsPublisher(); var doc = session.CurrentDocument!; doc.Signatures.Should().ContainSingle().Which.Role.Should().Be(SignatureRole.Publisher); doc.Metadata.SubmissionUrls.Should().Equal("https://gov/submit"); session.IsDirty.Should().BeTrue(); shell.HasSignatures.Should().BeTrue(); } finally { File.Delete(pfx); }
    }

    [Fact]
    public async Task SignMyResponses_SignsAnsweredFields()
    {
        var pfx = MakeTempPfx("Ada"); var fs = Substitute.For<IFileService>(); var dlg = Substitute.For<IDialogService>(); fs.PickCertificateAsync().Returns(pfx); dlg.ShowInputAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), true).Returns(""); var session = new DocumentSessionService(); var shell = CreateShell(fs, dialogService: dlg, session: session); var doc = MakeTemplate(); doc.Sections[0].Prompts[0].Response = "Ada Lovelace"; session.Set(doc, null);
        try { await shell.SignMyResponses(); var sig = session.CurrentDocument!.Signatures.Should().ContainSingle().Subject; sig.Role.Should().Be(SignatureRole.Filler); sig.Fields.Should().Contain("p1").And.NotContain("p2"); } finally { File.Delete(pfx); }
    }

    [Fact]
    public async Task SignAsPublisher_WhenCertPickerCancelled_AddsNothing()
    {
        var fs = Substitute.For<IFileService>(); fs.PickCertificateAsync().Returns((string?)null); var session = new DocumentSessionService(); var shell = CreateShell(fs, session: session); session.Set(MakeTemplate(), null);
        await shell.SignAsPublisher(); (session.CurrentDocument!.Signatures ?? new()).Should().BeEmpty();
    }

    [Fact]
    public async Task SignMyResponses_WithNoAnsweredFields_ShowsDialog_AndSignsNothing()
    {
        var fs = Substitute.For<IFileService>(); var dlg = Substitute.For<IDialogService>(); var session = new DocumentSessionService(); var shell = CreateShell(fs, dialogService: dlg, session: session); session.Set(MakeTemplate(), null);
        await shell.SignMyResponses(); await dlg.Received(1).ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>()); await fs.DidNotReceive().PickCertificateAsync(); (session.CurrentDocument!.Signatures ?? new()).Should().BeEmpty();
    }
}
