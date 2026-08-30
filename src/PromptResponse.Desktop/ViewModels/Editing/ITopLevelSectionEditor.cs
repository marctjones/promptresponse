using PromptResponse.Core.Models;

namespace PromptResponse.Desktop.ViewModels.Editing;

/// <summary>
/// Raw, undoable mutations of the document's top-level section collection.
/// Keeping this small contract beside structural commands prevents those commands
/// from depending on the desktop shell's unrelated presentation responsibilities.
/// </summary>
internal interface ITopLevelSectionEditor
{
    void ApplyMoveTopLevelSection(int fromIndex, int toIndex);
    void ApplyAddTopLevelSectionAt(int index, Section section, SectionViewModel viewModel);
    void ApplyRemoveTopLevelSection(SectionViewModel viewModel);
}
