namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// One advisory surfaced in the right rail. Each item carries the originating
/// prompt's id and visible label so the rail's UI can link back to the field
/// (click → scroll-to-prompt) and explain the reason via <see cref="Message"/>.
/// Vision invariant: advisories are never blocking — they're hints, not errors.
/// </summary>
public sealed record AdvisoryItem(string PromptId, string PromptLabel, string Message);
