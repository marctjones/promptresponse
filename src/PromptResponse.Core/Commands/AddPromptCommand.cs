using PromptResponse.Core.Models;

namespace PromptResponse.Core.Commands;

/// <summary>
/// Command to add a prompt to a section or subsection.
/// </summary>
public class AddPromptCommand : ICommand
{
    private readonly List<Prompt> _promptList;
    private readonly Prompt _prompt;
    private readonly int _index;
    private readonly string _containerName;

    public string Description => $"Add prompt '{_prompt.Label}' to {_containerName}";

    /// <summary>
    /// Initializes a new instance for adding a prompt to a section.
    /// </summary>
    public AddPromptCommand(Section section, Prompt prompt, int index = -1)
    {
        if (section == null) throw new ArgumentNullException(nameof(section));
        _promptList = section.Prompts;
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        _index = index < 0 ? _promptList.Count : index;
        _containerName = $"section '{section.Title}'";
    }

    /// <summary>
    /// Initializes a new instance for adding a prompt to a subsection.
    /// </summary>
    public AddPromptCommand(Subsection subsection, Prompt prompt, int index = -1)
    {
        if (subsection == null) throw new ArgumentNullException(nameof(subsection));
        _promptList = subsection.Prompts;
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        _index = index < 0 ? _promptList.Count : index;
        _containerName = $"subsection '{subsection.Title}'";
    }

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

    public void Undo()
    {
        _promptList.Remove(_prompt);
    }

    public bool CanMergeWith(ICommand other) => false;

    public void MergeWith(ICommand other) { }
}
