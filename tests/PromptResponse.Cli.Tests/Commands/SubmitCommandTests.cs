using System.Text;
using AwesomeAssertions;
using PromptResponse.Cli.Commands;
using PromptResponse.Core;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using PromptResponse.Host.Abstractions;
using Xunit;

namespace PromptResponse.Cli.Tests.Commands;

/// <summary>Submitting: what the command decides, and what it hands to the host.</summary>
/// <remarks>
/// Every one of these runs against a fake adapter and touches no network, which is the
/// reason the port exists. A port that opened a dialog or owned an HttpClient could not
/// be stood in for, and a submission test that reaches the network is a test of somebody
/// else's server.
/// </remarks>
public class SubmitCommandTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("apr-submit").FullName;
    private readonly IAprSerializer _serializer = new Beta6AprSerializer();

    private sealed class FakeDelivery : IDelivery
    {
        public List<(Uri Target, string MediaType, string Body)> Sent { get; } = [];

        public DeliveryResult Answer { get; set; } = DeliveryResult.Delivered("sent");

        public bool Supports(Uri target) =>
            target.Scheme == Uri.UriSchemeHttps || target.Scheme == "mailto";

        public Task<DeliveryResult> DeliverAsync(
            Uri target, ReadOnlyMemory<byte> document, string mediaType, string fileName,
            CancellationToken cancellationToken = default)
        {
            Sent.Add((target, mediaType, Encoding.UTF8.GetString(document.Span)));
            return Task.FromResult(Answer);
        }
    }

    private string Write(string name, params string[] submissionUrls)
    {
        var document = new AprDocument
        {
            Version = AprFormat.CurrentVersion,
            DocumentType = DocumentType.FilledForm,
            Metadata = new Metadata
            {
                Title = "Permit",
                TemplateId = "tag:example.com,2026:permit",
                SubmissionUrls = submissionUrls.Length == 0 ? null : [.. submissionUrls],
            },
            Sections = [new Section
            {
                Id = "s", Title = "S",
                Prompts = [new Prompt { Id = "p", Label = "P", Response = "Ada" }],
            }],
        };
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, _serializer.Serialize(document));
        return path;
    }

    private (SubmitCommand Command, FakeDelivery Delivery) Build()
    {
        var delivery = new FakeDelivery();
        return (new SubmitCommand(_serializer, new DocumentValidator(), delivery), delivery);
    }

    [Fact]
    public async Task WithoutConfirmation_NothingIsSent()
    {
        var (command, delivery) = Build();
        var path = Write("a.aprf", "https://example.gov/drop/a");

        var code = await command.ExecuteAsync([path]);

        code.Should().Be(2, "an unconfirmed submission is neither a success nor a failure");
        delivery.Sent.Should().BeEmpty("confirmation is the surface's decision, and it was not given");
    }

    [Fact]
    public async Task WithConfirmation_TheDocumentGoesToTheOneTargetItNames()
    {
        var (command, delivery) = Build();
        var path = Write("a.aprf", "https://example.gov/drop/a");

        var code = await command.ExecuteAsync([path, "--yes"]);

        code.Should().Be(0);
        delivery.Sent.Should().ContainSingle();
        delivery.Sent[0].Target.Should().Be(new Uri("https://example.gov/drop/a"));
        delivery.Sent[0].MediaType.Should().Be("application/vnd.apr+json",
            "the document is labelled with the media type its representation defines");
        delivery.Sent[0].Body.Should().Contain("Ada", "the document is the body, whole");
    }

    [Fact]
    public async Task WhereADocumentNamesSeveralTargets_TheCommandChoosesNone()
    {
        var (command, delivery) = Build();
        var path = Write("a.aprf", "https://example.gov/a", "https://example.gov/b");

        var code = await command.ExecuteAsync([path, "--yes"]);

        code.Should().Be(1, "choosing for somebody is choosing where their answers go");
        delivery.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ATargetTheDocumentDoesNotName_IsRefused()
    {
        var (command, delivery) = Build();
        var path = Write("a.aprf", "https://example.gov/a");

        var code = await command.ExecuteAsync([path, "--url=https://elsewhere.example/x", "--yes"]);

        code.Should().Be(1, "a submission target is the author's statement, not the sender's");
        delivery.Sent.Should().BeEmpty();
    }

    [Theory]
    [InlineData("ftp://example.gov/drop")]
    [InlineData("http://example.gov/drop")]
    public async Task ASchemeTheFormatDoesNotDefine_IsRefusedBeforeAnyAttempt(string url)
    {
        var (command, delivery) = Build();
        var path = Write("a.aprf", url);

        var code = await command.ExecuteAsync([path, "--yes"]);

        code.Should().Be(1);
        delivery.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task AnUnavailableCapability_IsNotAFailureOfTheDocument()
    {
        var (command, delivery) = Build();
        delivery.Answer = DeliveryResult.Unavailable("this host composes no mail");
        var path = Write("a.aprf", "mailto:forms@example.gov");

        var code = await command.ExecuteAsync([path, "--yes"]);

        code.Should().Be(3,
            "distinct from a refusal, so nobody edits a file that was never the problem");
    }

    [Fact]
    public async Task ARefusalByTheTarget_Fails()
    {
        var (command, delivery) = Build();
        delivery.Answer = DeliveryResult.Refused("HTTP 403 Forbidden");
        var path = Write("a.aprf", "https://example.gov/drop/a");

        (await command.ExecuteAsync([path, "--yes"])).Should().Be(1);
    }

    [Fact]
    public async Task ADocumentWithStructuralErrors_IsNotSent()
    {
        var (command, delivery) = Build();
        var path = Path.Combine(_directory, "broken.aprf");
        File.WriteAllText(path,
            "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},"
            + "\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[]}]}");

        var code = await command.ExecuteAsync([path, "--url=https://example.gov/a", "--yes"]);

        code.Should().Be(1);
        delivery.Sent.Should().BeEmpty("a document that is not valid is not ready to submit");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }
}
