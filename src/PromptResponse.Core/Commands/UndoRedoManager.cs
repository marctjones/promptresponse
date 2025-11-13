namespace PromptResponse.Core.Commands;

/// <summary>
/// Manages undo/redo operations using the command pattern.
/// </summary>
public class UndoRedoManager
{
    private readonly Stack<ICommand> _undoStack = new();
    private readonly Stack<ICommand> _redoStack = new();
    private readonly int _maxUndoLevels;

    /// <summary>
    /// Gets whether there are commands available to undo.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Gets whether there are commands available to redo.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Gets the number of commands in the undo stack.
    /// </summary>
    public int UndoCount => _undoStack.Count;

    /// <summary>
    /// Gets the number of commands in the redo stack.
    /// </summary>
    public int RedoCount => _redoStack.Count;

    /// <summary>
    /// Event raised when undo/redo state changes.
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="UndoRedoManager"/> class.
    /// </summary>
    /// <param name="maxUndoLevels">Maximum number of undo levels to maintain. Default is 100.</param>
    public UndoRedoManager(int maxUndoLevels = 100)
    {
        _maxUndoLevels = maxUndoLevels;
    }

    /// <summary>
    /// Executes a command and adds it to the undo stack.
    /// </summary>
    public void ExecuteCommand(ICommand command)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        // Check if we can merge with the last command
        if (_undoStack.Count > 0)
        {
            var lastCommand = _undoStack.Peek();
            if (lastCommand.CanMergeWith(command))
            {
                lastCommand.MergeWith(command);
                command.Execute();
                OnStateChanged();
                return;
            }
        }

        // Execute the command
        command.Execute();

        // Add to undo stack
        _undoStack.Push(command);

        // Limit stack size
        if (_undoStack.Count > _maxUndoLevels)
        {
            // Remove oldest command
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = 0; i < _maxUndoLevels; i++)
            {
                _undoStack.Push(items[i]);
            }
        }

        // Clear redo stack when new command is executed
        _redoStack.Clear();

        OnStateChanged();
    }

    /// <summary>
    /// Undoes the last command.
    /// </summary>
    public void Undo()
    {
        if (!CanUndo)
            throw new InvalidOperationException("Nothing to undo");

        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);

        OnStateChanged();
    }

    /// <summary>
    /// Redoes the last undone command.
    /// </summary>
    public void Redo()
    {
        if (!CanRedo)
            throw new InvalidOperationException("Nothing to redo");

        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);

        OnStateChanged();
    }

    /// <summary>
    /// Clears all undo/redo history.
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        OnStateChanged();
    }

    /// <summary>
    /// Gets the description of the command that would be undone.
    /// </summary>
    public string? GetUndoDescription()
    {
        return CanUndo ? _undoStack.Peek().Description : null;
    }

    /// <summary>
    /// Gets the description of the command that would be redone.
    /// </summary>
    public string? GetRedoDescription()
    {
        return CanRedo ? _redoStack.Peek().Description : null;
    }

    private void OnStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
