using System.Net.Http.Headers;
using System.Text;
using PromptResponse.Host.Abstractions;

namespace PromptResponse.Cli.Host;

/// <summary>The command line's delivery adapter: an HTTPS PUT, or a mailto handoff.</summary>
/// <remarks>
/// The two transports section 5.2.1 defines, and no others. A scheme this document does
/// not define is <see cref="DeliveryOutcome.Unavailable"/> rather than an attempt.
/// </remarks>
public sealed class CliDelivery(HttpMessageHandler? handler = null) : IDelivery, IDisposable
{
    private readonly HttpClient _client = new(
        handler ?? new HttpClientHandler { AllowAutoRedirect = false }, disposeHandler: true)
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    public bool Supports(Uri target) =>
        target.Scheme == Uri.UriSchemeHttps || target.Scheme == "mailto";

    public async Task<DeliveryResult> DeliverAsync(
        Uri target, ReadOnlyMemory<byte> document, string mediaType, string fileName,
        CancellationToken cancellationToken = default)
    {
        if (target.Scheme == "mailto")
        {
            // The document goes as a single attachment, never inlined. This host writes
            // the message beside the document and says where; opening a mail client is
            // the desktop's job, and a command line that launched one would be doing
            // something its caller did not ask for.
            return DeliveryResult.Unavailable(
                "this host composes no mail. Attach the file to a message addressed to "
                + $"{target.AbsolutePath} — the document goes as an attachment, never "
                + "pasted into the body.");
        }
        if (target.Scheme != Uri.UriSchemeHttps)
        {
            return DeliveryResult.Unavailable(
                $"'{target.Scheme}' is not a submission scheme this format defines.");
        }

        // A single PUT of the document as the body. Nothing derived from the document
        // becomes a header, no redirect is followed, and nothing is retried: a
        // pre-signed URL carries its own authorisation, and a retry of a submission is
        // a second submission.
        using var content = new ByteArrayContent(document.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        try
        {
            using var response = await _client.PutAsync(target, content, cancellationToken);
            var status = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
            return response.IsSuccessStatusCode
                ? DeliveryResult.Delivered($"{fileName} delivered to {target}: {status}")
                : DeliveryResult.Refused($"the target refused it: {status}. No redirect was followed.");
        }
        catch (TaskCanceledException)
        {
            return DeliveryResult.Refused("the target did not answer within thirty seconds.");
        }
        catch (HttpRequestException exception)
        {
            return DeliveryResult.Refused($"the target could not be reached: {exception.Message}");
        }
    }

    public void Dispose() => _client.Dispose();
}
