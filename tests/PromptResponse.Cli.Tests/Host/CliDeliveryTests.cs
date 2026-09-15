using System.Net;
using System.Text;
using AwesomeAssertions;
using PromptResponse.Cli.Host;
using PromptResponse.Host.Abstractions;
using Xunit;

namespace PromptResponse.Cli.Tests.Host;

/// <summary>
/// <c>CliDelivery</c> is the reference-correct implementation of specification 5.2.1
/// (a single HTTP PUT for HTTPS submission) and the model the desktop's
/// <c>HttpsSubmissionService</c> was fixed to match on 2026-09-07 — but had no test of
/// its own asserting the HTTP method it actually sends. This closes that gap.
/// </summary>
public class CliDeliveryTests
{
    private sealed class RecordingHandler(HttpStatusCode statusCode = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }
        public int Requests { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var response = new HttpResponseMessage(statusCode);
            if ((int)statusCode is >= 300 and < 400)
                response.Headers.Location = new Uri("https://elsewhere.example/other");
            return response;
        }
    }

    [Fact]
    public void TheDefaultHandler_FollowsNoRedirect()
    {
        // APR-MODEL-088, for the client this host actually builds.
        using var handler = CliDelivery.CreateHandler();

        handler.Should().BeOfType<HttpClientHandler>().Which.AllowAutoRedirect.Should().BeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task DeliverAsync_SendsOneRequest_AndNeverFollowsOrRetries(HttpStatusCode status)
    {
        // APR-MODEL-088 and APR-MODEL-089: a redirect or a failure ends the attempt; a second
        // request would be a second submission.
        var handler = new RecordingHandler(status);
        using var delivery = new CliDelivery(handler);

        var result = await delivery.DeliverAsync(
            new Uri("https://example.com/submit"), Encoding.UTF8.GetBytes("{}"), "application/vnd.apr+json", "form.aprf");

        result.Outcome.Should().Be(DeliveryOutcome.Refused);
        handler.Requests.Should().Be(1);
    }

    [Fact]
    public async Task DeliverAsync_SendsNoCredentialsOrHeadersDerivedFromTheDocument()
    {
        // APR-MODEL-086: nothing the document says becomes part of the request but its body.
        var handler = new RecordingHandler();
        using var delivery = new CliDelivery(handler);
        var document = Encoding.UTF8.GetBytes(
            "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"Authorization: Bearer secret\",\"author\":\"Cookie: session=1\"}}");

        await delivery.DeliverAsync(
            new Uri("https://example.com/submit"), document, "application/vnd.apr+json", "form.aprf");

        handler.LastRequest!.Headers.Should().BeEmpty();
        handler.LastRequest.Content!.Headers.Select(header => header.Key)
            .Should().BeSubsetOf(["Content-Type", "Content-Length"]);
    }

    [Fact]
    public async Task DeliverAsync_SendsAPut_NotAPost()
    {
        var handler = new RecordingHandler();
        using var delivery = new CliDelivery(handler);
        var document = Encoding.UTF8.GetBytes("{\"aprVersion\":\"1.0-beta.6\"}");

        await delivery.DeliverAsync(
            new Uri("https://example.com/submit"), document, "application/vnd.apr+json", "form.aprf");

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Put,
            "specification 5.2.1 (APR-MODEL-033) defines HTTPS submission as a single "
            + "PUT; a pre-signed browser POST is deliberately absent from the format");
    }

    [Fact]
    public async Task DeliverAsync_SendsTheDocumentBytesAndMediaTypeVerbatim()
    {
        var handler = new RecordingHandler();
        using var delivery = new CliDelivery(handler);
        var document = Encoding.UTF8.GetBytes("aprVersion: 1.0-beta.6\nsections: []\n");

        await delivery.DeliverAsync(
            new Uri("https://example.com/submit"), document, "application/vnd.apr+yaml", "form.apr.yaml");

        handler.LastRequestBody.Should().Be(Encoding.UTF8.GetString(document));
        handler.LastRequest!.Content!.Headers.ContentType!.MediaType
            .Should().Be("application/vnd.apr+yaml");
    }

    [Fact]
    public async Task DeliverAsync_ReportsDeliveredOn2xx()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created);
        using var delivery = new CliDelivery(handler);

        var result = await delivery.DeliverAsync(
            new Uri("https://example.com/submit"), ReadOnlyMemory<byte>.Empty, "application/vnd.apr+json", "form.aprf");

        result.Outcome.Should().Be(DeliveryOutcome.Delivered);
    }

    [Fact]
    public async Task DeliverAsync_ReportsRefusedOnNon2xx()
    {
        var handler = new RecordingHandler(HttpStatusCode.Forbidden);
        using var delivery = new CliDelivery(handler);

        var result = await delivery.DeliverAsync(
            new Uri("https://example.com/submit"), ReadOnlyMemory<byte>.Empty, "application/vnd.apr+json", "form.aprf");

        result.Outcome.Should().Be(DeliveryOutcome.Refused);
    }

    [Fact]
    public async Task DeliverAsync_ReportsUnavailableForAMailtoTarget_WithoutComposingMail()
    {
        var handler = new RecordingHandler();
        using var delivery = new CliDelivery(handler);

        var result = await delivery.DeliverAsync(
            new Uri("mailto:forms@example.com"), ReadOnlyMemory<byte>.Empty, "application/vnd.apr+json", "form.aprf");

        result.Outcome.Should().Be(DeliveryOutcome.Unavailable);
        handler.LastRequest.Should().BeNull("the CLI composes no mail itself");
    }

    [Theory]
    [InlineData("mailto:forms@example.com", "forms@example.com")]
    [InlineData("mailto:forms@example.com?subject=Permit", "forms@example.com")]
    public async Task DeliverAsync_NamesTheRealAddress_ForAMailtoTarget(string uri, string expectedAddress)
    {
        // Uri.AbsolutePath is empty for a mailto: URI -- it is not a hierarchical
        // scheme -- so the message must extract the address another way. This shipped
        // showing a blank address ("addressed to  — the document...") until fixed
        // alongside the podman submission receiver demo on 2026-09-07.
        using var delivery = new CliDelivery();

        var result = await delivery.DeliverAsync(
            new Uri(uri), ReadOnlyMemory<byte>.Empty, "application/vnd.apr+json", "form.aprf");

        result.Detail.Should().Contain(expectedAddress);
        result.Detail.Should().NotContain("addressed to  ", "the address must never render blank");
    }

    [Fact]
    public async Task DeliverAsync_ReportsUnavailableForAnUnrecognisedScheme_WithoutSendingAnything()
    {
        var handler = new RecordingHandler();
        using var delivery = new CliDelivery(handler);

        var result = await delivery.DeliverAsync(
            new Uri("ftp://example.com/submit"), ReadOnlyMemory<byte>.Empty, "application/vnd.apr+json", "form.aprf");

        result.Outcome.Should().Be(DeliveryOutcome.Unavailable);
        handler.LastRequest.Should().BeNull();
    }

    [Theory]
    [InlineData("https")]
    [InlineData("mailto")]
    public void Supports_RecognisesOnlyTheTwoTransportsTheSpecificationDefines(string scheme)
    {
        using var delivery = new CliDelivery();

        delivery.Supports(new Uri($"{scheme}:{(scheme == "mailto" ? "a@b.com" : "//example.com/submit")}"))
            .Should().BeTrue();
    }

    [Fact]
    public void Supports_RejectsASchemeTheSpecificationDoesNotDefine()
    {
        using var delivery = new CliDelivery();

        delivery.Supports(new Uri("ftp://example.com/submit")).Should().BeFalse();
    }
}
