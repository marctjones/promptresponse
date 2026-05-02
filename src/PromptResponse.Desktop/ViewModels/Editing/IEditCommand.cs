namespace PromptResponse.Desktop.ViewModels.Editing;

/// <summary>
/// One reversible mutation in the editor history. Implementations capture
/// enough inverse state at construction time that <see cref="Undo"/> can
/// faithfully reverse <see cref="Execute"/>. Both must mutate the model AND
/// the bound view-model tree so the UI re-syncs without an explicit refresh.
/// </summary>
/// <remarks>
/// Lifecycle: an <see cref="EditHistory"/> calls <see cref="Execute"/> when
/// recording the command initially, then on Redo. <see cref="Undo"/> reverses.
/// During Execute/Undo/Redo, the history sets <see cref="EditHistory.IsApplying"/>
/// so view-model setters that would otherwise record a new command know to do
/// the raw mutation only — preventing recursive command creation.
/// </remarks>
public interface IEditCommand
{
    /// <summary>Apply the mutation. Called for the initial record and for redo.</summary>
    void Execute();

    /// <summary>Reverse the mutation. State after Undo must equal state before
    /// the original Execute, including model + VM tree + any side-effects the
    /// host needs to know about (prompt subscription notifications, etc.).</summary>
    void Undo();

    /// <summary>User-visible description for menu hover ("Undo Add Prompt").</summary>
    string Description { get; }

    /// <summary>True if <paramref name="next"/> can be folded into this command —
    /// e.g., consecutive keystrokes on the same property within a short window.
    /// Structural commands return false (every Add/Remove is its own undo step).</summary>
    bool CanMergeWith(IEditCommand next);

    /// <summary>Folds <paramref name="next"/> into this command. Caller has
    /// already ensured <see cref="CanMergeWith"/> returned true.</summary>
    void MergeWith(IEditCommand next);
}
