using System.Collections.ObjectModel;
using System.ComponentModel;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels.Editing;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.ViewModels.Workflows;

/// <summary>
/// Owns the editable document tree shown by the desktop shell. This is the one
/// place where model sections, their view models, undoable top-level edits, and
/// dynamic table-cell prompt subscriptions are kept in sync.
/// </summary>
internal sealed class DocumentTreeWorkflow : ITopLevelSectionEditor, IDisposable
{
    private readonly IDocumentSessionService _session;
    private readonly PromptViewModelFactory _factory;
    private readonly EditHistory _history;
    private readonly Action<PromptViewModelBase, PropertyChangedEventArgs> _onPromptChanged;
    private readonly Action _onTreeChanged;
    private readonly Action _refreshWizardSections;
    private bool _isRebuilding;

    public DocumentTreeWorkflow(
        IDocumentSessionService session,
        PromptViewModelFactory factory,
        EditHistory history,
        Action<PromptViewModelBase, PropertyChangedEventArgs> onPromptChanged,
        Action onTreeChanged,
        Action refreshWizardSections)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _onPromptChanged = onPromptChanged ?? throw new ArgumentNullException(nameof(onPromptChanged));
        _onTreeChanged = onTreeChanged ?? throw new ArgumentNullException(nameof(onTreeChanged));
        _refreshWizardSections = refreshWizardSections ?? throw new ArgumentNullException(nameof(refreshWizardSections));
    }

    public ObservableCollection<PromptViewModelBase> Prompts { get; } = new();
    public ObservableCollection<SectionViewModel> Sections { get; } = new();

    public void Rebuild(AprDocument? document)
    {
        _isRebuilding = true;
        try
        {
            Clear();
            if (document is null) return;

            foreach (var section in document.Sections)
            {
                var sectionVm = CreateSectionViewModel(section);
                Sections.Add(sectionVm);
                CollectPrompts(sectionVm);
            }
        }
        finally
        {
            _isRebuilding = false;
            _onTreeChanged();
        }
    }

    public void AddTopLevelSection()
    {
        if (_session.CurrentDocument is null) return;
        var section = new Section
        {
            Id = $"section_{Guid.NewGuid():N}",
            Title = "New section",
        };
        // Sections require content, so authoring never creates an invalid empty shell.
        section.Prompts.Add(new Prompt { Id = $"{section.Id}.prompt_1", Label = "New prompt" });
        var viewModel = CreateSectionViewModel(section);
        var index = Sections.Count;

        if (_history.IsApplying)
            ApplyAddTopLevelSectionAt(index, section, viewModel);
        else
            _history.Execute(new AddTopLevelSectionCommand(this, section, viewModel, index));
    }

    public void RemoveTopLevelSection(SectionViewModel? sectionViewModel)
    {
        if (sectionViewModel is null || _session.CurrentDocument is null || !Sections.Contains(sectionViewModel)) return;
        if (_history.IsApplying)
            ApplyRemoveTopLevelSection(sectionViewModel);
        else
            _history.Execute(new RemoveTopLevelSectionCommand(this, sectionViewModel, Sections.IndexOf(sectionViewModel)));
    }

    public void MoveTopLevelSection(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex || _session.CurrentDocument is null ||
            fromIndex < 0 || fromIndex >= Sections.Count || toIndex < 0 || toIndex >= Sections.Count) return;
        if (_history.IsApplying)
            ApplyMoveTopLevelSection(fromIndex, toIndex);
        else
            _history.Execute(new MoveTopLevelSectionCommand(this, fromIndex, toIndex));
    }

    public void ApplyMoveTopLevelSection(int fromIndex, int toIndex)
    {
        var document = _session.CurrentDocument;
        if (document is null) return;
        var section = document.Sections[fromIndex];
        document.Sections.RemoveAt(fromIndex);
        document.Sections.Insert(toIndex, section);
        var viewModel = Sections[fromIndex];
        Sections.RemoveAt(fromIndex);
        Sections.Insert(toIndex, viewModel);
        _session.MarkDirty();
        _refreshWizardSections();
        NotifyTreeChanged();
    }

    public void ApplyAddTopLevelSectionAt(int index, Section section, SectionViewModel viewModel)
    {
        var document = _session.CurrentDocument;
        if (document is null) return;
        index = Math.Clamp(index, 0, document.Sections.Count);
        document.Sections.Insert(index, section);
        index = Math.Clamp(index, 0, Sections.Count);
        Sections.Insert(index, viewModel);
        TrackPromptTree(viewModel);
        _session.MarkDirty();
        _refreshWizardSections();
        NotifyTreeChanged();
    }

    public void ApplyRemoveTopLevelSection(SectionViewModel viewModel)
    {
        var document = _session.CurrentDocument;
        if (document is null || !Sections.Contains(viewModel)) return;
        UntrackPromptTree(viewModel);
        document.Sections.Remove(viewModel.Model);
        Sections.Remove(viewModel);
        _session.MarkDirty();
        _refreshWizardSections();
        NotifyTreeChanged();
    }

    private SectionViewModel CreateSectionViewModel(Section section) => new(
        section, _factory, depth: 0, onPromptAdded: TrackPrompt, onPromptRemoved: UntrackPrompt, history: _history);

    private void CollectPrompts(SectionViewModel section)
    {
        foreach (var prompt in section.PromptViewModels) TrackPrompt(prompt);
        foreach (var nested in section.NestedSections) CollectPrompts(nested);
    }

    private void TrackPromptTree(SectionViewModel section) => CollectPrompts(section);

    private void UntrackPromptTree(SectionViewModel section)
    {
        foreach (var prompt in section.PromptViewModels) UntrackPrompt(prompt);
        foreach (var nested in section.NestedSections) UntrackPromptTree(nested);
    }

    private void TrackPrompt(PromptViewModelBase prompt)
    {
        if (Prompts.Contains(prompt)) return;
        Prompts.Add(prompt);
        prompt.PropertyChanged += OnPromptPropertyChanged;
        NotifyTreeChanged();
    }

    private void UntrackPrompt(PromptViewModelBase prompt)
    {
        prompt.PropertyChanged -= OnPromptPropertyChanged;
        Prompts.Remove(prompt);
        prompt.Dispose();
        NotifyTreeChanged();
    }

    private void OnPromptPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is PromptViewModelBase prompt) _onPromptChanged(prompt, args);
    }

    private void NotifyTreeChanged()
    {
        if (!_isRebuilding) _onTreeChanged();
    }

    private void Clear()
    {
        foreach (var prompt in Prompts.ToArray()) UntrackPrompt(prompt);
        Sections.Clear();
    }

    public void Dispose() => Clear();
}
