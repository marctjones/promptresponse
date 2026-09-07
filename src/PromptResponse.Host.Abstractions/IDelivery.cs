namespace PromptResponse.Host.Abstractions;

/// <summary>Hands a completed document to a submission target the format defines.</summary>
/// <remarks>
/// One of the host ports decided in <c>docs/ARCHITECTURE.md</c>. Submission touches the
/// network or the desktop's mail client, and both are the host's business rather than
/// the format's.
///
/// Nothing crosses this port that is not already an APR artifact: the bytes to deliver,
/// the media type they carry, and the target. A port that took an <c>AprDocument</c>
/// would make every adapter a second implementation of the model.
///
/// A port returns a result and never opens a dialog. Whether to ask a person before
/// delivering is the calling surface's decision, and a port that asks cannot be tested
/// with a fake.
/// </remarks>
public interface IDelivery
{
    /// <summary>Is this target something this host can deliver to at all?</summary>
    /// <remarks>
    /// A capability may be absent — a headless runner has no mail client — and absent is
    /// an answer rather than an exception.
    /// </remarks>
    bool Supports(Uri target);

    /// <summary>Delivers <paramref name="document"/>, reporting what happened.</summary>
    Task<DeliveryResult> DeliverAsync(
        Uri target, ReadOnlyMemory<byte> document, string mediaType, string fileName,
        CancellationToken cancellationToken = default);
}

/// <summary>What a delivery attempt did, in terms a surface can report to a person.</summary>
/// <param name="Outcome">Delivered, refused by the target, or not something this host can do.</param>
/// <param name="Detail">
/// One line for a person. Never a platform API name, a stack trace, a credential, or a
/// path: a person told "the operating system returned E_NOTIMPL" has learned nothing they
/// can act on.
/// </param>
public sealed record DeliveryResult(DeliveryOutcome Outcome, string Detail)
{
    public static DeliveryResult Delivered(string detail) => new(DeliveryOutcome.Delivered, detail);

    public static DeliveryResult Refused(string detail) => new(DeliveryOutcome.Refused, detail);

    public static DeliveryResult Unavailable(string detail) => new(DeliveryOutcome.Unavailable, detail);
}

/// <summary>The three things a delivery attempt can come to.</summary>
public enum DeliveryOutcome
{
    /// <summary>The target accepted it.</summary>
    Delivered,

    /// <summary>The target was reached and said no. A person may be able to act on this.</summary>
    Refused,

    /// <summary>This host cannot do it. Not a failure of the document or the target.</summary>
    Unavailable,
}
