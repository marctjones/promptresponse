using PromptResponse.Core.Models;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels.Editing;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.ViewModels.Workflows;

/// <summary>
/// Owns the state transition that follows a session document change: replacing
/// metadata editing state, rebuilding the view-model tree, resetting undo and
/// wizard state, and initializing derived document behavior.
/// </summary>
/// <remarks>
/// The shell deliberately remains the binding façade. This coordinator makes
/// the lifecycle ordering explicit without moving Avalonia-facing property
/// names or generated commands out from beneath existing XAML bindings.
/// </remarks>
internal sealed class DocumentLifecycleCoordinator : IDisposable
{
    private readonly IDocumentSessionService _session;
    private readonly EditHistory _editHistory;
    private readonly DocumentTreeWorkflow _documentTree;
    private readonly FormProgressViewModel _progress;
    private readonly SearchViewModel _search;
    private readonly RoleSelectionWorkflow _roles;
    private readonly WizardProfileWorkflow _wizard;
    private readonly Action<AprDocument?> _applyExpressions;
    private readonly Action<bool> _setEditMode;

    public DocumentLifecycleCoordinator(
        IDocumentSessionService session,
        EditHistory editHistory,
        DocumentTreeWorkflow documentTree,
        FormProgressViewModel progress,
        SearchViewModel search,
        RoleSelectionWorkflow roles,
        WizardProfileWorkflow wizard,
        Action<AprDocument?> applyExpressions,
        Action<bool> setEditMode)
    {
        _session = session;
        _editHistory = editHistory;
        _documentTree = documentTree;
        _progress = progress;
        _search = search;
        _roles = roles;
        _wizard = wizard;
        _applyExpressions = applyExpressions;
        _setEditMode = setEditMode;
    }

    public DocumentMetadataViewModel? Metadata { get; private set; }

    public event Action<DocumentLifecycleChange>? StateChanged;

    public void HandleDocumentChanged(AprDocument? document)
    {
        _progress.SetDocument(document);
        _search.SetDocument(document);

        DisposeMetadata();
        _editHistory.Clear();
        _documentTree.Rebuild(document);

        if (document != null)
        {
            Metadata = new DocumentMetadataViewModel(document.Metadata, _editHistory);
            Metadata.Changed += OnMetadataChanged;
            _roles.Apply(document);
            _applyExpressions(document);
        }

        _setEditMode(_session.Mode == DocumentMode.EditingTemplate);
        _wizard.ResetForDocument();
        StateChanged?.Invoke(DocumentLifecycleChange.Document);
    }

    private void OnMetadataChanged(object? sender, EventArgs e)
    {
        _session.MarkDirty();
        StateChanged?.Invoke(DocumentLifecycleChange.Metadata);
    }

    private void DisposeMetadata()
    {
        if (Metadata != null)
        {
            Metadata.Changed -= OnMetadataChanged;
            Metadata = null;
        }
    }

    public void Dispose() => DisposeMetadata();
}

[Flags]
internal enum DocumentLifecycleChange
{
    Document = 1,
    Metadata = 2,
}
