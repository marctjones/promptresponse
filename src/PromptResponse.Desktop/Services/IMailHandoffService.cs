namespace PromptResponse.Desktop.Services;

/// <summary>
/// Opens a user-controlled email draft for a completed APR file. This is a local
/// handoff only: implementations must never send mail or make network requests.
/// </summary>
public interface IMailHandoffService
{
    Task<MailHandoffResult> ComposeAsync(MailHandoffRequest request, CancellationToken cancellationToken = default);
}

public sealed record MailHandoffRequest(string MailtoTarget, string AttachmentPath, string Subject, string Body);

public sealed record MailHandoffResult(bool DraftOpened, bool AttachmentWasHandedOff, string Message);
