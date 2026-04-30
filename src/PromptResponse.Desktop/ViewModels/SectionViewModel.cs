using System.Collections.ObjectModel;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// View-model wrapper for a <see cref="Section"/>. Mirrors the recursive section
/// hierarchy in the rendering tree: each section carries its title, description,
/// nested child sections (recursively), and the typed prompt VMs created via
/// <see cref="PromptViewModelFactory"/>.
/// </summary>
public sealed class SectionViewModel
{
    public SectionViewModel(Section section, PromptViewModelFactory factory, int depth)
    {
        if (section == null) throw new ArgumentNullException(nameof(section));
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        Id = section.Id;
        Title = section.Title;
        Description = section.Description;
        Depth = depth;

        var prompts = new List<PromptViewModelBase>(section.Prompts.Count);
        foreach (var prompt in section.Prompts)
        {
            prompts.Add(factory.Create(prompt));
        }
        PromptViewModels = new ReadOnlyCollection<PromptViewModelBase>(prompts);

        var nested = new List<SectionViewModel>(section.Sections.Count);
        foreach (var child in section.Sections)
        {
            nested.Add(new SectionViewModel(child, factory, depth + 1));
        }
        NestedSections = new ReadOnlyCollection<SectionViewModel>(nested);
    }

    public string Id { get; }
    public string Title { get; }
    public string? Description { get; }
    public int Depth { get; }
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    /// <summary>Indent depth in pixels — proportional to nesting depth.</summary>
    public double IndentLeft => Depth * 24.0;

    public IReadOnlyList<PromptViewModelBase> PromptViewModels { get; }
    public IReadOnlyList<SectionViewModel> NestedSections { get; }
}
