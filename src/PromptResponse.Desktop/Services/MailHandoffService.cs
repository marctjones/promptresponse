using System.Diagnostics;
using System.Globalization;
using System.Net.Mail;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// Best-effort integration with the user's mail application. Mail clients do
/// not expose one portable "default compose with attachment" protocol: mailto
/// carries the recipient and message fields, Apple Mail and Linux's xdg-email
/// can also receive an attachment. Every other platform receives an explicit
/// attach-it-yourself fallback after the draft is opened.
/// </summary>
public sealed class MailHandoffService : IMailHandoffService
{
    public async Task<MailHandoffResult> ComposeAsync(MailHandoffRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetRecipient(request.MailtoTarget, out var recipient))
        {
            return new MailHandoffResult(false, false,
                "This form's email destination is not a safe plain mailto: recipient.");
        }
        if (!File.Exists(request.AttachmentPath))
        {
            return new MailHandoffResult(false, false,
                "The completed APR file could not be found, so no email draft was opened.");
        }

        var composeUri = $"mailto:{Uri.EscapeDataString(recipient)}?subject={Uri.EscapeDataString(request.Subject)}&body={Uri.EscapeDataString(request.Body)}";

        if (OperatingSystem.IsMacOS())
        {
            try
            {
                using var process = Process.Start(CreateAppleMailStartInfo(recipient, request));
                if (process is not null)
                {
                    await process.WaitForExitAsync(cancellationToken);
                    if (process.ExitCode == 0)
                    {
                        return new MailHandoffResult(true, true,
                            "An Apple Mail draft was opened with the completed APR file attached.");
                    }
                }
                // Apple Mail may be missing, disabled, or automation permission may
                // have been denied. In all cases, retain a usable default-mail fallback.
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                // Fall through to mailto. The generic path needs no Apple Mail permission.
            }
        }

        // xdg-email is the only cross-desktop convention that accepts an attachment.
        // Do not use a shell: the file path and target are data, never commands.
        if (OperatingSystem.IsLinux())
        {
            try
            {
                var start = new ProcessStartInfo("xdg-email") { UseShellExecute = false };
                start.ArgumentList.Add("--attach");
                start.ArgumentList.Add(request.AttachmentPath);
                start.ArgumentList.Add(composeUri);
                Process.Start(start);
                return new MailHandoffResult(true, true,
                    "An email draft was opened with the completed APR file attached.");
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Fall through to mailto. Some minimal Linux desktops do not ship xdg-email.
            }
        }

        try
        {
            Process.Start(new ProcessStartInfo(composeUri) { UseShellExecute = true });
            return new MailHandoffResult(true, false,
                $"An email draft was opened. Attach {Path.GetFileName(request.AttachmentPath)} before sending.");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new MailHandoffResult(false, false,
                $"Could not open the default email application. Attach {Path.GetFileName(request.AttachmentPath)} manually.");
        }
    }

    /// <summary>
    /// Apple Mail is deliberately an optional platform enhancement, not a generic
    /// mailto replacement. Values are supplied through argv so neither APR data nor
    /// a filename can become executable AppleScript source.
    /// </summary>
    public const string AppleMailScript = """
        on run argv
            if (count of argv) is not 4 then error "Expected recipient, subject, body, and attachment path"
            set recipientAddress to item 1 of argv
            set subjectText to item 2 of argv
            set bodyText to item 3 of argv
            set attachmentPath to item 4 of argv
            tell application "Mail"
                set draftMessage to make new outgoing message with properties {visible:true, subject:subjectText, content:bodyText}
                tell draftMessage
                    make new to recipient at end of to recipients with properties {address:recipientAddress}
                    make new attachment with properties {file name:(POSIX file attachmentPath)} at after the last paragraph
                end tell
                activate
            end tell
        end run
        """;

    private static ProcessStartInfo CreateAppleMailStartInfo(string recipient, MailHandoffRequest request)
    {
        var start = new ProcessStartInfo("/usr/bin/osascript") { UseShellExecute = false };
        start.ArgumentList.Add("-e");
        start.ArgumentList.Add(AppleMailScript);
        start.ArgumentList.Add(recipient);
        start.ArgumentList.Add(request.Subject);
        start.ArgumentList.Add(request.Body);
        start.ArgumentList.Add(request.AttachmentPath);
        return start;
    }

    /// <summary>Only a bare recipient is accepted from an APR document. In particular,
    /// form-supplied cc, bcc, subject and body query parameters are never honoured.</summary>
    public static bool TryGetRecipient(string? target, out string recipient)
    {
        recipient = string.Empty;
        if (string.IsNullOrWhiteSpace(target) || !target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return false;
        var raw = target[7..];
        if (raw.Length == 0 || raw.Contains('?') || raw.Contains('#') || raw.Any(IsUnsafeTargetCharacter) || raw.Any(char.IsWhiteSpace)) return false;

        try
        {
            var address = new MailAddress(raw);
            if (!string.Equals(address.Address, raw, StringComparison.OrdinalIgnoreCase)) return false;
            recipient = address.Address;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsUnsafeTargetCharacter(char character) =>
        char.IsControl(character) || char.GetUnicodeCategory(character) is UnicodeCategory.Format or UnicodeCategory.Surrogate or UnicodeCategory.OtherNotAssigned;
}
