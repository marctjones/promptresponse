using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PromptResponse.Desktop.ViewModels.Editing;

/// <summary>
/// Stack-based undo/redo history for the editor. Records every mutation as an
/// <see cref="IEditCommand"/>; Undo pops the top of the undo stack, replays
/// its inverse, and pushes onto the redo stack. Any new Execute clears the
/// redo stack — branching on undo is the standard editor convention.
/// </summary>
/// <remarks>
/// Sets <see cref="IsApplying"/> while running Execute/Undo/Redo so view-model
/// setters and methods that route through this history know to do the raw
/// mutation only — preventing recursive command creation when an undo
/// changes a property that itself wired up a recording setter.
///
/// Consecutive same-target/same-property property-edit commands within a
/// short window get merged via <see cref="IEditCommand.CanMergeWith"/> so a
/// 20-keystroke title rename collapses to one undo step.
/// </remarks>
public sealed class EditHistory : INotifyPropertyChanged
{
    private readonly LinkedList<IEditCommand> _undo = new();
    private readonly Stack<IEditCommand> _redo = new();
    private readonly int _maxLevels;

    public EditHistory(int maxLevels = 100)
    {
        if (maxLevels < 1) throw new ArgumentOutOfRangeException(nameof(maxLevels));
        _maxLevels = maxLevels;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>True while a command is being applied (initial Execute, Undo,
    /// or Redo). VM setters check this so undo-driven mutations don't record
    /// a new command on top of the one being applied.</summary>
    public bool IsApplying { get; private set; }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public string? UndoDescription => _undo.Count > 0 ? _undo.Last!.Value.Description : null;
    public string? RedoDescription => _redo.Count > 0 ? _redo.Peek().Description : null;

    /// <summary>Execute a new command and record it for undo. If the new command
    /// can merge with the top of the undo stack (e.g. consecutive property-edit
    /// keystrokes on the same target), it folds in instead of pushing.</summary>
    public void Execute(IEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplyWithFlag(command.Execute);

        if (_undo.Count > 0 && _undo.Last!.Value.CanMergeWith(command))
        {
            _undo.Last.Value.MergeWith(command);
        }
        else
        {
            _undo.AddLast(command);
            // Trim oldest if we exceed the max-levels cap.
            while (_undo.Count > _maxLevels) _undo.RemoveFirst();
        }
        _redo.Clear();
        RaiseAllChanged();
    }

    /// <summary>Pop the top command off the undo stack and replay its Undo.</summary>
    public void Undo()
    {
        if (_undo.Count == 0) return;
        var c = _undo.Last!.Value;
        _undo.RemoveLast();
        ApplyWithFlag(c.Undo);
        _redo.Push(c);
        RaiseAllChanged();
    }

    /// <summary>Pop the top command off the redo stack and replay its Execute.</summary>
    public void Redo()
    {
        if (_redo.Count == 0) return;
        var c = _redo.Pop();
        ApplyWithFlag(c.Execute);
        _undo.AddLast(c);
        RaiseAllChanged();
    }

    /// <summary>Drop both stacks. Called on document load so old document's
    /// undo history doesn't leak into the new document.</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        RaiseAllChanged();
    }

    private void ApplyWithFlag(Action action)
    {
        IsApplying = true;
        try { action(); }
        finally { IsApplying = false; }
    }

    private void RaiseAllChanged()
    {
        Notify(nameof(CanUndo));
        Notify(nameof(CanRedo));
        Notify(nameof(UndoDescription));
        Notify(nameof(RedoDescription));
    }

    private void Notify([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
