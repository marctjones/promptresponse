namespace PromptResponse.GuiSubmitDemo.Avalonia;

/// <summary>
/// Writes the exact bytes of the outgoing PUT body to disk before sending it, so a caller
/// (demo.sh) can diff what the real GUI actually put on the wire against what MinIO ends up
/// holding — the same kind of check the CLI demo does with the file it already has on disk.
/// The GUI has no such file: <c>DocumentDeliveryWorkflow</c> serializes the in-session
/// document fresh for each submission, so this is the only place those bytes exist.
/// </summary>
internal sealed class CapturingHandler(HttpMessageHandler inner, string capturePath)
    : DelegatingHandler(inner)
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is { } content)
        {
            var bytes = await content.ReadAsByteArrayAsync(cancellationToken);
            await File.WriteAllBytesAsync(capturePath, bytes, cancellationToken);
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
