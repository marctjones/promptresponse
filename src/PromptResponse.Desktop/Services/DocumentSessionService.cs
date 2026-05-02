using PromptResponse.Core.Models;

namespace PromptResponse.Desktop.Services;

/// <summary>
/// High-level mode the user is in based on the current document type.
/// </summary>
public enum DocumentMode
{
    None,
    EditingTemplate,
    FillingForm,
}

/// <summary>
/// Owns the active document's lifecycle: open / save / close, dirty tracking, and
/// derived state (title, mode). Kept as a focused service so the shell stays
/// thin and the contract is testable in isolation.
/// </summary>
public interface IDocumentSessionService
{
    AprDocument? CurrentDocument { get; }
    string? CurrentFilePath { get; }
    bool HasDocument { get; }
    bool IsDirty { get; }
    DocumentMode Mode { get; }
    string Title { get; }

    /// <summary>Replace the active document. Raises <see cref="DocumentChanged"/>.</summary>
    void Set(AprDocument document, string? filePath, bool dirty = false);

    /// <summary>Mark the current document as having unsaved changes.</summary>
    void MarkDirty();

    /// <summary>Mark the current document as clean (typically after save).</summary>
    void MarkClean();

    /// <summary>Close the current document. Clears state and raises events.</summary>
    void Close();

    /// <summary>Raised whenever <see cref="CurrentDocument"/> changes (including to null on Close).</summary>
    event EventHandler<AprDocument?>? DocumentChanged;

    /// <summary>Raised whenever <see cref="IsDirty"/> transitions.</summary>
    event EventHandler<bool>? DirtyChanged;
}

/// <inheritdoc cref="IDocumentSessionService"/>
public sealed class DocumentSessionService : IDocumentSessionService
{
    private const string AppName = "PromptResponse";

    public AprDocument? CurrentDocument { get; private set; }
    public string? CurrentFilePath { get; private set; }
    public bool HasDocument => CurrentDocument != null;
    public bool IsDirty { get; private set; }

    public DocumentMode Mode => CurrentDocument?.DocumentType switch
    {
        DocumentType.Template => DocumentMode.EditingTemplate,
        DocumentType.FilledForm => DocumentMode.FillingForm,
        _ => DocumentMode.None,
    };

    public string Title
    {
        get
        {
            if (CurrentDocument == null) return AppName;
            var prefix = IsDirty ? "• " : string.Empty;
            var docTitle = string.IsNullOrWhiteSpace(CurrentDocument.Metadata.Title)
                ? "Untitled"
                : CurrentDocument.Metadata.Title;
            return $"{prefix}{docTitle} — {AppName}";
        }
    }

    public event EventHandler<AprDocument?>? DocumentChanged;
    public event EventHandler<bool>? DirtyChanged;

    public void Set(AprDocument document, string? filePath, bool dirty = false)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        CurrentDocument = document;
        CurrentFilePath = filePath;
        var dirtyChanged = IsDirty != dirty;
        IsDirty = dirty;
        DocumentChanged?.Invoke(this, document);
        if (dirtyChanged) DirtyChanged?.Invoke(this, IsDirty);
    }

    public void MarkDirty()
    {
        if (IsDirty) return;
        IsDirty = true;
        DirtyChanged?.Invoke(this, true);
    }

    public void MarkClean()
    {
        if (!IsDirty) return;
        IsDirty = false;
        DirtyChanged?.Invoke(this, false);
    }

    public void Close()
    {
        var wasDirty = IsDirty;
        CurrentDocument = null;
        CurrentFilePath = null;
        IsDirty = false;
        DocumentChanged?.Invoke(this, null);
        if (wasDirty) DirtyChanged?.Invoke(this, false);
    }
}
