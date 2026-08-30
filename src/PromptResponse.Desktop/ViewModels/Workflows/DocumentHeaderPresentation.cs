using PromptResponse.Core.Models;
using PromptResponse.Desktop.Services;

namespace PromptResponse.Desktop.ViewModels.Workflows;

/// <summary>
/// Presents session and completion state at the document boundary.
/// </summary>
/// <remarks>
/// The session deliberately owns document lifetime and dirty state, while
/// <see cref="FormProgressViewModel"/> owns completion counts. This small
/// presentation object is the only place that turns those two domain values
/// into the human-facing header, mode, and status-bar vocabulary. Keeping it
/// out of the shell prevents bindings from re-implementing session semantics.
/// </remarks>
internal sealed class DocumentHeaderPresentation
{
    private readonly IDocumentSessionService _session;
    private readonly FormProgressViewModel _progress;

    public DocumentHeaderPresentation(
        IDocumentSessionService session,
        FormProgressViewModel progress)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
    }

    public bool HasDocument => _session.HasDocument;
    public bool IsFilledForm => _session.Mode == DocumentMode.FillingForm;
    public bool IsEditingTemplate => _session.Mode == DocumentMode.EditingTemplate;
    public bool IsEmptyState => !HasDocument;
    public DocumentMode Mode => _session.Mode;
    public string Title => _session.Title;
    public string CurrentDocumentTitle => _session.CurrentDocument?.Metadata.Title ?? string.Empty;
    public string? DocumentDescription => _session.CurrentDocument?.Metadata.Description;
    public bool HasDocumentDescription => !string.IsNullOrWhiteSpace(DocumentDescription);

    /// <summary>Human-readable document mode; source enum identifiers never reach the UI.</summary>
    public string ModeDescription => Mode switch
    {
        DocumentMode.EditingTemplate => "Editing template",
        DocumentMode.FillingForm => "Filling in",
        _ => string.Empty,
    };

    /// <summary>Filling attribution for the document header, when it is available.</summary>
    public string? FilledByDisplay
    {
        get
        {
            var metadata = _session.CurrentDocument?.Metadata;
            if (metadata == null || Mode != DocumentMode.FillingForm) return null;

            var filledBy = string.IsNullOrWhiteSpace(metadata.FilledBy) ? null : metadata.FilledBy;
            var filledDate = metadata.FilledDate?.ToString("MMMM d, yyyy");
            return (filledBy, filledDate) switch
            {
                (not null, not null) => $"Filled by {filledBy} on {filledDate}",
                (not null, null) => $"Filled by {filledBy}",
                (null, not null) => $"Filled on {filledDate}",
                _ => null,
            };
        }
    }

    /// <summary>Polite live-region message combining the active title and completion state.</summary>
    public string StatusMessage => HasDocument
        ? $"{CurrentDocumentTitle} — {_progress.StatusText}"
        : "No document open. Use File → New, or File → Open to get started.";
}
