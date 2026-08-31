using AwesomeAssertions;
using PromptResponse.Cli.Commands;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Signing;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands;

public sealed class Beta6CommandTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"apr-beta6-cli-{Guid.NewGuid():N}");

    public Beta6CommandTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task Validate_AcceptsJsoncAndReportsEveryStreamRecord()
    {
        var path = Path.Combine(_directory, "stream.apr");
        var form = "{\"version\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[]}]}";
        var attestation = """{"recordType":"attestation","version":"1.0-beta.6","subject":{"digest":"sha256:0000000000000000000000000000000000000000000000000000000000000000","canonicalization":"jcs-sha256"},"scope":{"kind":"document"},"manifest":{"root":"sha256:0000000000000000000000000000000000000000000000000000000000000000","entries":[]},"proofs":[],"witnesses":[]}""";
        await File.WriteAllTextAsync(path, "\u001e" + attestation + "\n\u001e" + form);

        var result = await new Beta6Command().ExecuteAsync(["validate", path]);

        result.Should().Be(0);
    }

    [Fact]
    public async Task Normalize_UsesRequestedYamlOutput()
    {
        var source = Path.Combine(_directory, "form.apr");
        var output = Path.Combine(_directory, "form.yaml");
        await File.WriteAllTextAsync(source, "// comment\n{\"version\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[]}]}");

        var result = await new Beta6Command().ExecuteAsync(["normalize", source, "--yaml", "--output=" + output]);

        result.Should().Be(0);
        (await File.ReadAllTextAsync(output)).Should().Contain("version: 1.0-beta.6");
    }

    [Fact]
    public async Task Inspect_ReportsAttestationStateWithoutSelectingAForm()
    {
        var path = Path.Combine(_directory, "stream.apr");
        var form = "{\"version\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[]}]}";
        var attestation = """{"recordType":"attestation","version":"1.0-beta.6","subject":{"digest":"sha256:0000000000000000000000000000000000000000000000000000000000000000","canonicalization":"jcs-sha256"},"scope":{"kind":"document"},"manifest":{"root":"sha256:0000000000000000000000000000000000000000000000000000000000000000","entries":[]},"proofs":[],"witnesses":[]}""";
        await File.WriteAllTextAsync(path, "\u001e" + attestation + "\n\u001e" + form);

        var result = await new Beta6Command().ExecuteAsync(["inspect", path, "--json"]);

        result.Should().Be(0);
    }

    [Fact]
    public async Task Attest_AppendsAnIndependentCmsRecord()
    {
        var source = Path.Combine(_directory, "form.apr");
        var output = Path.Combine(_directory, "attested.apr");
        var pfx = Path.Combine(_directory, "signer.pfx");
        await File.WriteAllTextAsync(source, """{"version":"1.0-beta.6","metadata":{"title":"T"},"sections":[{"id":"s","title":"S","prompts":[{"id":"p","label":"P","response":"Ada"}]}]}""");
        using (var certificate = SignatureCertificates.CreateSelfSigned("Ada", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1)))
            await File.WriteAllBytesAsync(pfx, certificate.Export(X509ContentType.Pfx, "secret"));

        var result = await new AttestCommand().ExecuteAsync([source, "--cert=" + pfx, "--password=secret", "--output=" + output, "--fields=p"]);

        result.Should().Be(0);
        var records = new AprBeta6Reader().ReadStream(await File.ReadAllTextAsync(output), AprRepresentation.Jsonc);
        records.Should().HaveCount(2);
        AprAttestationResolver.Resolve(records).Single().State.Should().Be(AprAttestationState.Valid);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
