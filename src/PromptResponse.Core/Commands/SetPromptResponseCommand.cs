using PromptResponse.Core.Models;

namespace PromptResponse.Core.Commands;

/// <summary>
/// Command to set a prompt's response value.
/// </summary>
public class SetPromptResponseCommand : ICommand
{
    private readonly Prompt _prompt;
    private readonly string _newValue;
    private string _oldValue;
    private DateTime? _oldLastModified;

    public string Description => $"Set response for '{_prompt.Label}'";

    public SetPromptResponseCommand(Prompt prompt, string newValue)
    {
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        _newValue = newValue;
        _oldValue = prompt.Response;
        _oldLastModified = prompt.ResponseMetadata.LastModified;
    }

    public void Execute()
    {
        _prompt.Response = _newValue;
    }

    public void Undo()
    {
        _prompt.Response = _oldValue;
        _prompt.ResponseMetadata.LastModified = _oldLastModified;
    }

    public bool CanMergeWith(ICommand other)
    {
        // Merge consecutive text edits to the same prompt
        if (other is SetPromptResponseCommand otherCommand &&
            otherCommand._prompt == _prompt)
        {
            // Only merge if the changes are within a short time window (e.g., 2 seconds)
            var timeDiff = DateTime.UtcNow - (_prompt.ResponseMetadata.LastModified ?? DateTime.UtcNow);
            return timeDiff.TotalSeconds < 2;
        }
        return false;
    }

    public void MergeWith(ICommand other)
    {
        if (other is SetPromptResponseCommand otherCommand)
        {
            // Keep the original old value, but update to the new value from the other command
            _newValue = otherCommand._newValue;
        }
    }
}
