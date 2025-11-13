using PromptResponse.Core.Models;

namespace PromptResponse.Core.Commands;

/// <summary>
/// Command to set a prompt's response value.
/// </summary>
public class SetPromptResponseCommand : ICommand
{
    private readonly Prompt _prompt;
    private string _newValue;
    private string _oldValue;
    private DateTime? _oldLastModified;
    private readonly DateTime _createdAt;

    /// <inheritdoc/>
    public string Description => $"Set response for '{_prompt.Label}'";

    /// <summary>
    /// Initializes a new instance of the <see cref="SetPromptResponseCommand"/> class.
    /// </summary>
    /// <param name="prompt">The prompt to modify.</param>
    /// <param name="newValue">The new response value.</param>
    public SetPromptResponseCommand(Prompt prompt, string newValue)
    {
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        _newValue = newValue;
        _oldValue = prompt.Response;
        _oldLastModified = prompt.ResponseMetadata.LastModified;
        _createdAt = DateTime.UtcNow;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _prompt.Response = _newValue;
        _prompt.ResponseMetadata.LastModified = DateTime.UtcNow;
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _prompt.Response = _oldValue;
        _prompt.ResponseMetadata.LastModified = _oldLastModified;
    }

    /// <inheritdoc/>
    public bool CanMergeWith(ICommand other)
    {
        // Merge consecutive text edits to the same prompt
        if (other is SetPromptResponseCommand otherCommand &&
            otherCommand._prompt == _prompt)
        {
            // Only merge if the changes are within a short time window (50ms)
            // This catches rapid typing but not distinct user actions
            var timeDiff = otherCommand._createdAt - _createdAt;
            return timeDiff.TotalMilliseconds < 50;
        }
        return false;
    }

    /// <inheritdoc/>
    public void MergeWith(ICommand other)
    {
        if (other is SetPromptResponseCommand otherCommand)
        {
            // Keep the original old value, but update to the new value from the other command
            _newValue = otherCommand._newValue;
        }
    }
}
