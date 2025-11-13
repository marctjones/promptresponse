namespace PromptResponse.Core.Commands;

/// <summary>
/// Interface for undoable commands.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Executes the command.
    /// </summary>
    void Execute();

    /// <summary>
    /// Undoes the command, reverting to the previous state.
    /// </summary>
    void Undo();

    /// <summary>
    /// Gets a description of this command for display purposes.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets whether this command can be merged with another command.
    /// Used for combining consecutive similar operations (e.g., typing).
    /// </summary>
    bool CanMergeWith(ICommand other);

    /// <summary>
    /// Merges this command with another command.
    /// </summary>
    void MergeWith(ICommand other);
}
