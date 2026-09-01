using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PromptResponse.Core.Models;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// Tracks completion progress on a filled form. Pure derived state — no UI
/// dependencies, fully unit-testable. Composed into the shell so progress is
/// owned by a small focused VM, not the host.
/// </summary>
public sealed partial class FormProgressViewModel : ObservableObject
{
    [ObservableProperty]
    private int totalPrompts;

    [ObservableProperty]
    private int answeredPrompts;

    [ObservableProperty]
    private int percentComplete;

    [ObservableProperty]
    private string statusText = string.Empty;

    /// <summary>Top-level section completion, in document order. Each entry owns its nested children.</summary>
    public ObservableCollection<SectionProgress> Sections { get; } = new();

    private AprDocument? _document;

    /// <summary>Re-binds progress tracking to a new document. Pass null to clear.</summary>
    public void SetDocument(AprDocument? document)
    {
        _document = document;
        Refresh();
    }

    /// <summary>Recomputes progress from the current document. Call after any prompt mutation.</summary>
    public void Refresh()
    {
        var (total, answered) = Count(_document);
        TotalPrompts = total;
        AnsweredPrompts = answered;
        PercentComplete = total == 0 ? 0 : (int)Math.Round(100.0 * answered / total);
        StatusText = total == 0
            ? "No prompts"
            : $"{answered} of {total} answered ({PercentComplete}%)";

        Sections.Clear();
        if (_document != null)
        {
            for (var index = 0; index < _document.Sections.Count; index++)
            {
                Sections.Add(BuildSectionProgress(_document.Sections[index], depth: 0, topLevelIndex: index));
            }
        }
    }

    private static SectionProgress BuildSectionProgress(Section section, int depth, int topLevelIndex)
    {
        int total = 0, answered = 0;
        CountSection(section, ref total, ref answered);
        var progress = new SectionProgress(section.Title, answered, total)
        {
            SectionId = section.Id,
            Depth = depth,
            TopLevelIndex = topLevelIndex,
        };

        foreach (var child in section.Sections)
            progress.Children.Add(BuildSectionProgress(child, depth + 1, topLevelIndex));
        return progress;
    }

    private static (int total, int answered) Count(AprDocument? document)
    {
        if (document == null) return (0, 0);
        int total = 0, answered = 0;
        foreach (var section in document.Sections)
        {
            CountSection(section, ref total, ref answered);
        }
        return (total, answered);
    }

    private static void CountSection(Section section, ref int total, ref int answered)
    {
        foreach (var prompt in section.Prompts)
        {
            total++;
            if (!string.IsNullOrWhiteSpace(prompt.Response))
            {
                answered++;
            }
        }
        foreach (var nested in section.Sections)
        {
            CountSection(nested, ref total, ref answered);
        }
    }
}

/// <summary>Completion of a single top-level section.</summary>
/// <param name="Title">The section title.</param>
/// <param name="Answered">Answered prompts (including nested).</param>
/// <param name="Total">Total prompts (including nested).</param>
public sealed partial class SectionProgress : ObservableObject
{
    public SectionProgress(string title, int answered, int total)
    {
        Title = title;
        Answered = answered;
        Total = total;
    }

    public string Title { get; }
    public int Answered { get; }
    public int Total { get; }

    /// <summary>Stable APR section identity used by sidebar navigation.</summary>
    public string SectionId { get; init; } = string.Empty;

    /// <summary>Depth in the document's section tree.</summary>
    public int Depth { get; init; }

    /// <summary>Containing top-level section, used when wizard mode is active.</summary>
    public int TopLevelIndex { get; init; }

    /// <summary>Nested section completion entries.</summary>
    public ObservableCollection<SectionProgress> Children { get; } = new();

    public bool HasChildren => Children.Count > 0;

    /// <summary>Whether this branch is exposed in the progress sidebar.</summary>
    [ObservableProperty]
    private bool isExpanded;

    public string ExpansionGlyph => IsExpanded ? "▾" : "▸";

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(ExpansionGlyph));

    /// <summary>Completion percentage (0 when the section has no prompts).</summary>
    public int PercentComplete => Total == 0 ? 0 : (int)Math.Round(100.0 * Answered / Total);

    /// <summary>True when every prompt in the section is answered.</summary>
    public bool IsComplete => Total > 0 && Answered == Total;

    /// <summary>Short "answered/total" label, with a check when complete.</summary>
    public string StatusText => Total == 0 ? "—" : IsComplete ? $"✓ {Answered}/{Total}" : $"{Answered}/{Total}";
}
