using PromptResponse.Core.Models;

namespace PromptResponse.Core.Commands;

/// <summary>
/// Command to remove a section from a document.
/// </summary>
public class RemoveSectionCommand : ICommand
{
    private readonly AprDocument _document;
    private readonly Section _section;
    private int _originalIndex;

    /// <inheritdoc/>
    public string Description => $"Remove section '{_section.Title}'";

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveSectionCommand"/> class.
    /// </summary>
    /// <param name="document">The document to remove the section from.</param>
    /// <param name="section">The section to remove.</param>
    public RemoveSectionCommand(AprDocument document, Section section)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _section = section ?? throw new ArgumentNullException(nameof(section));
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _originalIndex = _document.Sections.IndexOf(_section);
        if (_originalIndex < 0)
            throw new InvalidOperationException("Section not found in document");

        _document.Sections.RemoveAt(_originalIndex);
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _document.Sections.Insert(_originalIndex, _section);
    }

    /// <inheritdoc/>
    public bool CanMergeWith(ICommand other) => false;

    /// <inheritdoc/>
    public void MergeWith(ICommand other) { }
}
