using System.Diagnostics.Tracing;

namespace PromptResponse.RendererDriver.Avalonia;

/// <summary>Records every network request the process makes while it is listening.</summary>
/// <remarks>
/// `APR-SEC-010` says opening a document contacts nothing: a `submissionUrls` entry is
/// data until somebody acts on it. A driver that simply reported an empty `requests` would
/// be asserting that rather than observing it, which is the echo the harness's mutant
/// drivers exist to catch. The runtime's own event sources see every HTTP request, DNS
/// lookup and socket connect regardless of which library made it, so this watches instead.
/// </remarks>
internal sealed class NetworkWatch : EventListener
{
    private static readonly string[] Sources =
        ["System.Net.Http", "System.Net.Sockets", "System.Net.NameResolution"];

    private readonly List<string> _requests = [];
    private readonly Lock _gate = new();

    internal IReadOnlyList<string> Since(int mark)
    {
        lock (_gate) return _requests.Skip(mark).ToArray();
    }

    internal int Mark()
    {
        lock (_gate) return _requests.Count;
    }

    protected override void OnEventSourceCreated(EventSource source)
    {
        if (Sources.Contains(source.Name))
            EnableEvents(source, EventLevel.Informational);
    }

    protected override void OnEventWritten(EventWrittenEventArgs written)
    {
        // The event names differ per source; what matters is that something reached out.
        if (written.EventName is not ("RequestStart" or "ConnectStart" or "ResolutionStart"))
            return;
        var detail = string.Join(" ", (written.Payload ?? []).Select(value => value?.ToString()));
        lock (_gate) _requests.Add($"{written.EventSource.Name}: {detail}".Trim());
    }
}
