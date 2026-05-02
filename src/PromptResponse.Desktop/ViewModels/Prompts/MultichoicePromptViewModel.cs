using System.Collections.ObjectModel;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.ViewModels.Editing;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Multi-choice (multi-select) prompt. The stored response is a comma-separated
/// string of selected values; the user can also free-type any value. View renders
/// the suggested values as checkboxes plus an optional "Other" text input.
/// </summary>
public sealed class MultichoicePromptViewModel : PromptViewModelBase
{
    public MultichoicePromptViewModel(Prompt prompt, IProfileService profileService, EditHistory? history = null)
        : base(prompt, profileService, history)
    {
        SuggestedValues = new ReadOnlyCollection<string>(prompt.Hints.SuggestedValues ?? new List<string>());
    }

    public IReadOnlyList<string> SuggestedValues { get; }

    /// <summary>Whether a given suggested value is currently in the response set.</summary>
    public bool IsSelected(string value)
    {
        var selected = SplitResponse();
        return selected.Contains(value);
    }

    public void Select(string value)
    {
        var selected = SplitResponse().ToList();
        if (!selected.Contains(value))
        {
            selected.Add(value);
            Response = string.Join(", ", selected);
        }
    }

    public void Deselect(string value)
    {
        var selected = SplitResponse().Where(v => v != value).ToList();
        Response = string.Join(", ", selected);
    }

    private IEnumerable<string> SplitResponse()
    {
        if (string.IsNullOrWhiteSpace(Response)) return Array.Empty<string>();
        return Response.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0);
    }
}
