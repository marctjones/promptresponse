using PromptResponse.Core.Models;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.ViewModels.Editing;

/// <summary>
/// Coordinates non-table structural edits for one <see cref="SectionViewModel"/>.
/// </summary>
/// <remarks>
/// This owns the paired model/view-model collection mutations and their undo policy.
/// <see cref="SectionViewModel"/> retains its public command-facing surface, while
/// table-specific mutations remain in <see cref="TableMutationCoordinator"/>.
/// </remarks>
internal sealed class SectionStructureCoordinator
{
    private readonly SectionViewModel _owner;

    internal SectionStructureCoordinator(SectionViewModel owner) => _owner = owner;

    private Section Section => _owner.Model;
    private EditHistory? History => _owner.History;

    internal PromptViewModelBase AddPrompt()
    {
        var prompt = new Prompt
        {
            Id = $"prompt_{Guid.NewGuid():N}",
            Label = "New prompt",
            Hints = new PromptHints { ExpectedDataType = "text" },
        };
        var viewModel = _owner.CreatePromptViewModel(prompt);
        var index = _owner.PromptViewModels.Count;

        if (History is { IsApplying: false } history)
            history.Execute(new AddPromptCommand(_owner, prompt, viewModel, index));
        else
            ApplyAddPromptAt(index, prompt, viewModel);

        return viewModel;
    }

    internal void RemovePrompt(PromptViewModelBase? viewModel)
    {
        if (viewModel is null || !_owner.PromptViewModels.Contains(viewModel)) return;
        if (WouldLeaveSectionEmpty(removingPrompts: 1)) return;

        if (History is { IsApplying: false } history)
            history.Execute(new RemovePromptCommand(_owner, viewModel, _owner.PromptViewModels.IndexOf(viewModel)));
        else
            ApplyRemovePrompt(viewModel);
    }

    internal SectionViewModel AddNestedSection()
    {
        var sectionId = $"section_{Guid.NewGuid():N}";
        var model = new Section
        {
            Id = sectionId,
            Title = "New section",
            Prompts = [new Prompt { Id = $"{sectionId}.prompt_1", Label = "New prompt" }],
        };
        var viewModel = _owner.CreateChildSectionViewModel(model);
        var index = _owner.NestedSections.Count;

        if (History is { IsApplying: false } history)
            history.Execute(new AddNestedSectionCommand(_owner, viewModel, index));
        else
            ApplyAddNestedSectionAt(index, model, viewModel);

        return viewModel;
    }

    internal void RemoveNestedSection(SectionViewModel? child)
    {
        if (child is null || !_owner.NestedSections.Contains(child)) return;
        if (_owner.IsTableSection && _owner.NestedSections.Count <= 1) return;

        if (History is { IsApplying: false } history)
            history.Execute(new RemoveNestedSectionCommand(_owner, child, _owner.NestedSections.IndexOf(child)));
        else
            ApplyRemoveNestedSection(child);
    }

    internal void MovePrompt(int fromIndex, int toIndex)
    {
        if (!IsValidMove(fromIndex, toIndex, _owner.PromptViewModels.Count)) return;
        if (History is { IsApplying: false } history)
            history.Execute(new MovePromptCommand(_owner, fromIndex, toIndex));
        else
            ApplyMovePrompt(fromIndex, toIndex);
    }

    internal void MoveNestedSection(int fromIndex, int toIndex)
    {
        if (!IsValidMove(fromIndex, toIndex, _owner.NestedSections.Count)) return;
        if (History is { IsApplying: false } history)
            history.Execute(new MoveNestedSectionCommand(_owner, fromIndex, toIndex));
        else
            ApplyMoveNestedSection(fromIndex, toIndex);
    }

    internal void ApplyMovePrompt(int fromIndex, int toIndex)
    {
        var prompt = Section.Prompts[fromIndex];
        Section.Prompts.RemoveAt(fromIndex);
        Section.Prompts.Insert(toIndex, prompt);
        var viewModel = _owner.PromptViewModels[fromIndex];
        _owner.PromptViewModels.RemoveAt(fromIndex);
        _owner.PromptViewModels.Insert(toIndex, viewModel);
    }

    internal void ApplyMoveNestedSection(int fromIndex, int toIndex)
    {
        var section = Section.Sections[fromIndex];
        Section.Sections.RemoveAt(fromIndex);
        Section.Sections.Insert(toIndex, section);
        var viewModel = _owner.NestedSections[fromIndex];
        _owner.NestedSections.RemoveAt(fromIndex);
        _owner.NestedSections.Insert(toIndex, viewModel);
    }

    internal void ApplyAddPromptAt(int index, Prompt prompt, PromptViewModelBase viewModel)
    {
        if (index < 0 || index > Section.Prompts.Count) index = Section.Prompts.Count;
        Section.Prompts.Insert(index, prompt);
        if (index > _owner.PromptViewModels.Count) index = _owner.PromptViewModels.Count;
        _owner.PromptViewModels.Insert(index, viewModel);
        _owner.NotifyPromptAdded(viewModel);
    }

    internal void ApplyRemovePrompt(PromptViewModelBase viewModel)
    {
        if (!_owner.PromptViewModels.Contains(viewModel)) return;
        _owner.NotifyPromptRemoved(viewModel);
        Section.Prompts.Remove(viewModel.Model);
        _owner.PromptViewModels.Remove(viewModel);
    }

    internal void ApplyAddNestedSectionAt(int index, Section model, SectionViewModel viewModel)
    {
        if (index < 0 || index > Section.Sections.Count) index = Section.Sections.Count;
        Section.Sections.Insert(index, model);
        if (index > _owner.NestedSections.Count) index = _owner.NestedSections.Count;
        _owner.NestedSections.Insert(index, viewModel);
        WalkPrompts(viewModel, _owner.NotifyPromptAdded);
    }

    internal void ApplyRemoveNestedSection(SectionViewModel child)
    {
        if (!_owner.NestedSections.Contains(child)) return;
        WalkPrompts(child, _owner.NotifyPromptRemoved);
        Section.Sections.Remove(child.Model);
        _owner.NestedSections.Remove(child);
    }

    private bool WouldLeaveSectionEmpty(int removingPrompts = 0, int removingNestedSections = 0) =>
        _owner.PromptViewModels.Count - removingPrompts <= 0
        && _owner.NestedSections.Count - removingNestedSections <= 0;

    private static bool IsValidMove(int fromIndex, int toIndex, int count) =>
        fromIndex != toIndex && fromIndex >= 0 && fromIndex < count && toIndex >= 0 && toIndex < count;

    private static void WalkPrompts(SectionViewModel section, Action<PromptViewModelBase> visit)
    {
        foreach (var prompt in section.PromptViewModels) visit(prompt);
        foreach (var child in section.NestedSections) WalkPrompts(child, visit);
    }
}
