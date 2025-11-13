using PromptResponse.Core.Models;

namespace PromptResponse.Core.Commands;

/// <summary>
/// Command to add a section to a document.
/// </summary>
public class AddSectionCommand : ICommand
{
    private readonly AprDocument _document;
    private readonly Section _section;
    private readonly int _index;

    public string Description => $"Add section '{_section.Title}'";

    public AddSectionCommand(AprDocument document, Section section, int index = -1)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _section = section ?? throw new ArgumentNullException(nameof(section));
        _index = index < 0 ? document.Sections.Count : index;
    }

    public void Execute()
    {
        if (_index >= _document.Sections.Count)
        {
            _document.Sections.Add(_section);
        }
        else
        {
            _document.Sections.Insert(_index, _section);
        }
    }

    public void Undo()
    {
        _document.Sections.Remove(_section);
    }

    public bool CanMergeWith(ICommand other) => false;

    public void MergeWith(ICommand other) { }
}
