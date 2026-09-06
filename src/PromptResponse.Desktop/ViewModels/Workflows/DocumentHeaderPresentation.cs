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

    /// <summary>Filling attribution for the document header. Always absent in beta.6.</summary>
    /// <remarks>
    /// `filledBy` and `filledDate` were retired as workflow state: an unsigned claim
    /// about who completed a form and when is not evidence of either, and showing one in
    /// a header presents it as though it were. A workflow that needs to record receipt
    /// writes an ordinary form naming this one under `metadata.regarding`, and an
    /// attestation is what makes such a claim provable (specification 5.2.2).
    ///
    /// The property remains so the header binding and its tests keep one place to
    /// change if a provable attribution is ever surfaced here.
    /// </remarks>
    public string? FilledByDisplay => null;

    /// <summary>Polite live-region message combining the active title and completion state.</summary>
    public string StatusMessage => HasDocument
        ? $"{CurrentDocumentTitle} — {_progress.StatusText}"
        : "No document open. Use File → New, or File → Open to get started.";
}
