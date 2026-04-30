using CommunityToolkit.Mvvm.ComponentModel;
using PromptResponse.Core.Models;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// Tracks completion progress on a filled form. Pure derived state — no UI
/// dependencies, fully unit-testable. Composed into <c>FormFillingViewModel</c>
/// to remove the progress logic from the legacy god class.
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
