using PromptResponse.Core.Models;

namespace PromptResponse.Core.Commands;

/// <summary>
/// Command to add a prompt to a section.
/// </summary>
public class AddPromptCommand : ICommand
{
    private readonly List<Prompt> _promptList;
    private readonly Prompt _prompt;
    private readonly int _index;
    private readonly string _containerName;

    /// <inheritdoc/>
    public string Description => $"Add prompt '{_prompt.Label}' to {_containerName}";

    /// <summary>
    /// Initializes a new instance for adding a prompt to a section.
    /// </summary>
    /// <param name="section">The section to add the prompt to.</param>
    /// <param name="prompt">The prompt to add.</param>
    /// <param name="index">The index at which to insert the prompt. Use -1 to append to the end.</param>
    public AddPromptCommand(Section section, Prompt prompt, int index = -1)
    {
        if (section == null) throw new ArgumentNullException(nameof(section));
        _promptList = section.Prompts;
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        _index = index < 0 ? _promptList.Count : index;
        _containerName = $"section '{section.Title}'";
    }

    /// <inheritdoc/>
    public void Execute()
    {
        if (_index >= _promptList.Count)
        {
            _promptList.Add(_prompt);
        }
        else
        {
            _promptList.Insert(_index, _prompt);
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _promptList.Remove(_prompt);
    }

    /// <inheritdoc/>
    public bool CanMergeWith(ICommand other) => false;

    /// <inheritdoc/>
    public void MergeWith(ICommand other) { }
}
