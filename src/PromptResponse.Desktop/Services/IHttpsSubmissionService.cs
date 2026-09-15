using System.Net.Http.Headers;

namespace PromptResponse.Desktop.Services;

public interface IHttpsSubmissionService
{
    Task<HttpsSubmissionResult> SubmitAsync(
        string target, string aprJson, CancellationToken cancellationToken = default);
}

public sealed record HttpsSubmissionResult(bool Succeeded, string Message);

/// <summary>The desktop's HTTPS submission target: a single pre-signed PUT.</summary>
/// <remarks>
/// Specification 5.2.1 (<c>APR-MODEL-033</c>) defines HTTPS submission as exactly one
/// HTTP PUT of the document as the request body — "a pre-signed browser POST is
/// deliberately absent," because a pre-signed URL is an S3-style PUT contract, and
/// every S3-compatible store accepts a PUT. This sent POST until 2026-09-07, which
/// meant a real pre-signed target would refuse it: nothing exercised this class against
/// an actual HTTP method, only against a mocked <see cref="IHttpsSubmissionService"/>
/// that never observed which verb reached the wire.
///
/// The CLI's <c>CliDelivery</c> already gets this right and is the reference for the
/// contract: PUT, no redirect followed, any non-2xx treated as failure, no retry. This
/// class duplicates that contract for the desktop rather than sharing an implementation,
/// per the host-port architecture decision (docs/ARCHITECTURE.md): each host keeps its
/// own composition root and its own adapter. Retiring this in favor of routing the
/// desktop through <c>IDelivery</c> the way the CLI does is tracked separately (#144);
/// this fix corrects the wire behavior without waiting on that migration.
/// </remarks>
public sealed class HttpsSubmissionService(HttpMessageHandler? handler = null)
    : IHttpsSubmissionService, IDisposable
{
    private readonly HttpClient _client = new(handler ?? CreateHandler(), disposeHandler: true)
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    /// <summary>The handler this service sends through when none is supplied.</summary>
    /// <remarks>A redirect is never followed (APR-MODEL-088): a pre-signed target that
    /// answers elsewhere has not accepted the document.</remarks>
    internal static HttpMessageHandler CreateHandler() => new HttpClientHandler { AllowAutoRedirect = false };

    public async Task<HttpsSubmissionResult> SubmitAsync(
        string target, string aprJson, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            return new HttpsSubmissionResult(false, "This is not a safe HTTPS submission target.");
        }

        // A single PUT of the document as the body, exactly as CliDelivery sends it.
        // Nothing derived from the document becomes a header, no redirect is followed
        // (the handler above disables it), and a failure is never retried automatically:
        // a pre-signed URL carries its own authorisation and expiry, and a retry of a
        // submission is a second submission.
        using var content = new StringContent(aprJson);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.apr+json");
        try
        {
            using var response = await _client.PutAsync(uri, content, cancellationToken);
            return response.IsSuccessStatusCode
                ? new HttpsSubmissionResult(
                    true, $"Submitted to {uri}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}")
                : new HttpsSubmissionResult(
                    false,
                    $"Submission failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. "
                    + "No redirect was followed.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new HttpsSubmissionResult(false, $"Submission failed: {ex.Message}");
        }
    }

    public void Dispose() => _client.Dispose();
}
