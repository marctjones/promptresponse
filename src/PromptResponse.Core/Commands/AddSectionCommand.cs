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

    /// <inheritdoc/>
    public string Description => $"Add section '{_section.Title}'";

    /// <summary>
    /// Initializes a new instance of the <see cref="AddSectionCommand"/> class.
    /// </summary>
    /// <param name="document">The document to add the section to.</param>
    /// <param name="section">The section to add.</param>
    /// <param name="index">The index at which to insert the section. Use -1 to append to the end.</param>
    public AddSectionCommand(AprDocument document, Section section, int index = -1)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _section = section ?? throw new ArgumentNullException(nameof(section));
        _index = index < 0 ? document.Sections.Count : index;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public void Undo()
    {
        _document.Sections.Remove(_section);
    }

    /// <inheritdoc/>
    public bool CanMergeWith(ICommand other) => false;

    /// <inheritdoc/>
    public void MergeWith(ICommand other) { }
}
