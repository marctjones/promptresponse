using System.Net;
using AwesomeAssertions;
using PromptResponse.Desktop.Services;
using Xunit;

namespace PromptResponse.Desktop.Tests.Services;

/// <summary>
/// Exercises the actual HTTP method and headers <see cref="HttpsSubmissionService"/>
/// puts on the wire, using a fake <see cref="HttpMessageHandler"/> rather than a mocked
/// <see cref="IHttpsSubmissionService"/> — the earlier tests only ever mocked the
/// interface, which is exactly why this class sent POST for as long as it did without
/// any test noticing (specification 5.2.1, <c>APR-MODEL-033</c> requires PUT).
/// </summary>
public class HttpsSubmissionServiceTests
{
    private sealed class RecordingHandler(HttpStatusCode statusCode = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode);
        }
    }

    [Fact]
    public async Task SubmitAsync_SendsAPut_NotAPost()
    {
        var handler = new RecordingHandler();
        using var service = new HttpsSubmissionService(handler);

        await service.SubmitAsync("https://example.com/submit", "{\"aprVersion\":\"1.0-beta.6\"}");

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Put,
            "specification 5.2.1 (APR-MODEL-033) defines HTTPS submission as a single "
            + "PUT; a pre-signed browser POST is deliberately absent from the format");
    }

    [Fact]
    public async Task SubmitAsync_SendsTheDocumentBodyAndAprMediaType()
    {
        var handler = new RecordingHandler();
        using var service = new HttpsSubmissionService(handler);
        const string document = "{\"aprVersion\":\"1.0-beta.6\",\"sections\":[]}";

        await service.SubmitAsync("https://example.com/submit", document);

        handler.LastRequestBody.Should().Be(document);
        handler.LastRequest!.Content!.Headers.ContentType!.MediaType
            .Should().Be("application/vnd.apr+json");
    }

    [Fact]
    public async Task SubmitAsync_TreatsA2xxStatusAsSucceeded()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created);
        using var service = new HttpsSubmissionService(handler);

        var result = await service.SubmitAsync("https://example.com/submit", "{}");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitAsync_TreatsANon2xxStatusAsFailed()
    {
        var handler = new RecordingHandler(HttpStatusCode.Forbidden);
        using var service = new HttpsSubmissionService(handler);

        var result = await service.SubmitAsync("https://example.com/submit", "{}");

        result.Succeeded.Should().BeFalse();
    }

    [Theory]
    [InlineData("http://example.com/submit")]
    [InlineData("https://user:pass@example.com/submit")]
    [InlineData("https://example.com/submit#fragment")]
    [InlineData("not a url")]
    public async Task SubmitAsync_RefusesAnUnsafeTarget_WithoutSendingAnything(string target)
    {
        var handler = new RecordingHandler();
        using var service = new HttpsSubmissionService(handler);

        var result = await service.SubmitAsync(target, "{}");

        result.Succeeded.Should().BeFalse();
        handler.LastRequest.Should().BeNull("an unsafe target must never reach the network");
    }
}
