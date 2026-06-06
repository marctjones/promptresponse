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

    /// <summary>Per-top-level-section completion, in document order.</summary>
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
            foreach (var section in _document.Sections)
            {
                int st = 0, sa = 0;
                CountSection(section, ref st, ref sa);
                Sections.Add(new SectionProgress(section.Title, sa, st));
            }
        }
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
public sealed record SectionProgress(string Title, int Answered, int Total)
{
    /// <summary>Completion percentage (0 when the section has no prompts).</summary>
    public int PercentComplete => Total == 0 ? 0 : (int)Math.Round(100.0 * Answered / Total);

    /// <summary>True when every prompt in the section is answered.</summary>
    public bool IsComplete => Total > 0 && Answered == Total;

    /// <summary>Short "answered/total" label, with a check when complete.</summary>
    public string StatusText => Total == 0 ? "—" : IsComplete ? $"✓ {Answered}/{Total}" : $"{Answered}/{Total}";
}

