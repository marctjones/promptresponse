using PromptResponse.Core.Models;

namespace PromptResponse.Core.Commands;

/// <summary>
/// Command to remove a prompt from a section or subsection.
/// </summary>
public class RemovePromptCommand : ICommand
{
    private readonly List<Prompt> _promptList;
    private readonly Prompt _prompt;
    private int _originalIndex;
    private readonly string _containerName;

    public string Description => $"Remove prompt '{_prompt.Label}' from {_containerName}";

    /// <summary>
    /// Initializes a new instance for removing a prompt from a section.
    /// </summary>
    public RemovePromptCommand(Section section, Prompt prompt)
    {
        if (section == null) throw new ArgumentNullException(nameof(section));
        _promptList = section.Prompts;
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        _containerName = $"section '{section.Title}'";
    }

    /// <summary>
    /// Initializes a new instance for removing a prompt from a subsection.
    /// </summary>
    public RemovePromptCommand(Subsection subsection, Prompt prompt)
    {
        if (subsection == null) throw new ArgumentNullException(nameof(subsection));
        _promptList = subsection.Prompts;
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        _containerName = $"subsection '{subsection.Title}'";
    }

    public void Execute()
    {
        _originalIndex = _promptList.IndexOf(_prompt);
        if (_originalIndex < 0)
            throw new InvalidOperationException("Prompt not found");

        _promptList.RemoveAt(_originalIndex);
    }

    public void Undo()
    {
        _promptList.Insert(_originalIndex, _prompt);
    }

    public bool CanMergeWith(ICommand other) => false;

    public void MergeWith(ICommand other) { }
}
