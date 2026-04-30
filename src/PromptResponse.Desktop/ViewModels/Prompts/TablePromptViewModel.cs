using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.ViewModels.Prompts;

/// <summary>
/// Tabular prompt: rows × columns of values. The stored response is a JSON object
/// (fixed rows) or array-of-objects (dynamic rows) where every leaf is a string.
/// Per the vision, the schema captures structure only — no display info — and any
/// cell value is acceptable as text.
/// </summary>
public sealed class TablePromptViewModel : PromptViewModelBase
{
    private readonly TableDefinition? _definition;

    public TablePromptViewModel(Prompt prompt, IProfileService profileService)
        : base(prompt, profileService)
    {
        _definition = prompt.Hints.TableDefinition;
    }

    /// <summary>Structural definition of the table (columns, rows). Null when not configured.</summary>
    public TableDefinition? Definition => _definition;

    public bool IsFixedTable => _definition?.IsFixedTable ?? false;
    public bool IsDynamicTable => _definition?.IsDynamicTable ?? false;
}
