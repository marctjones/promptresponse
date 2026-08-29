using AwesomeAssertions;
using PromptResponse.Cli.Commands;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands;

/// <summary>
/// End-to-end tests for the signing CLI: keygen → sign (publisher + filler) →
/// verify, including trust reporting and tamper detection.
/// </summary>
public class SigningCommandsTests : IDisposable
{
    private readonly AprJsonSerializer _serializer = new();
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"apr-sign-{Guid.NewGuid():N}");

    public SigningCommandsTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private string Path_(string name) => System.IO.Path.Combine(_dir, name);

    private async Task WriteTemplate(string path)
    {
        var doc = new AprDocument
        {
            DocumentType = DocumentType.Template,
            Metadata = new Metadata { Title = "Permit", TemplateId = "permit", TemplateVersion = "1.0" },
            Sections =
            [
                new Section { Id = "s", Title = "Applicant", Prompts =
                [
                    new Prompt { Id = "name", Label = "Name" },
                    new Prompt { Id = "email", Label = "Email" },
                ]},
            ],
        };
        await File.WriteAllTextAsync(path, _serializer.Serialize(doc));
    }

    [Fact]
    public async Task Keygen_WritesPfxAndPublicCert()
    {
        var pfx = Path_("k.pfx");
        var cer = Path_("k.cer");

        var exit = await new KeygenCommand().ExecuteAsync(new[] { "--name=Town of Bloomfield", $"--output={pfx}", $"--cert-out={cer}" });

        exit.Should().Be(0);
        File.Exists(pfx).Should().BeTrue();
        File.Exists(cer).Should().BeTrue();
    }

    [Fact]
    public async Task PublisherSign_ThenVerify_PinnedIsTrusted()
    {
        var form = Path_("form.aprt");
        var pfx = Path_("pub.pfx");
        var cer = Path_("pub.cer");
        await WriteTemplate(form);
        await new KeygenCommand().ExecuteAsync(new[] { "--name=Town of Bloomfield", $"--output={pfx}", $"--cert-out={cer}" });

        var sign = await new SignCommand(_serializer).ExecuteAsync(
            new[] { form, "--publisher", $"--cert={pfx}", "--url=https://gov/submit" });
        sign.Should().Be(0);

        // The signature + bound URL landed in the file.
        var signed = _serializer.Deserialize(await File.ReadAllTextAsync(form));
        signed.Signatures.Should().ContainSingle();
        signed.Metadata.SubmissionUrls.Should().Equal("https://gov/submit");

        var verify = await new VerifyCommand(_serializer).ExecuteAsync(new[] { form, $"--trust={cer}" });
        verify.Should().Be(0, "a pinned publisher signature over unaltered content verifies");
    }

    [Fact]
    public async Task FillerSign_ThenTamper_VerifyFails()
    {
        var form = Path_("form.aprt");
        var pfx = Path_("ada.pfx");
        await WriteTemplate(form);
        await new KeygenCommand().ExecuteAsync(new[] { "--name=Ada", $"--output={pfx}" });

        // Put a response in, then sign it.
        var doc = _serializer.Deserialize(await File.ReadAllTextAsync(form));
        doc.Sections[0].Prompts[0].Response = "Ada Lovelace";
        await File.WriteAllTextAsync(form, _serializer.Serialize(doc));
        (await new SignCommand(_serializer).ExecuteAsync(new[] { form, "--fields=name", $"--cert={pfx}", "--id=ada" }))
            .Should().Be(0);

        (await new VerifyCommand(_serializer).ExecuteAsync(new[] { form }))
            .Should().Be(0, "the unaltered signed content verifies");

        // Tamper with the signed response.
        var tampered = _serializer.Deserialize(await File.ReadAllTextAsync(form));
        tampered.Sections[0].Prompts[0].Response = "Mallory";
        await File.WriteAllTextAsync(form, _serializer.Serialize(tampered));

        (await new VerifyCommand(_serializer).ExecuteAsync(new[] { form }))
            .Should().Be(1, "verify must fail (exit 1) when signed content was altered");
    }

    [Fact]
    public async Task Sign_WithoutPublisherOrFields_IsAnError()
    {
        var form = Path_("form.aprt");
        var pfx = Path_("k.pfx");
        await WriteTemplate(form);
        await new KeygenCommand().ExecuteAsync(new[] { "--name=X", $"--output={pfx}" });

        (await new SignCommand(_serializer).ExecuteAsync(new[] { form, $"--cert={pfx}" }))
            .Should().Be(1);
    }
}
