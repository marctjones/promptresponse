using PromptResponse.Core.Models;

namespace PromptResponse.Desktop.ViewModels.Editing;

/// <summary>
/// Creates independent table snapshot models for undo/redo. Kept separate from
/// the view-model so clone policy is explicit and reusable by table editing.
/// </summary>
internal static class TableSnapshotCloner
{
    internal static Section CloneSection(Section section) => new()
    {
        Id = section.Id,
        Title = section.Title,
        Description = section.Description,
        Kind = section.Kind,
        CanAddRows = section.CanAddRows,
        MaxRows = section.MaxRows,
        Prompts = section.Prompts.Select(ClonePrompt).ToList(),
        Sections = section.Sections.Select(CloneSection).ToList(),
    };

    internal static Prompt ClonePrompt(Prompt prompt) => new()
    {
        Id = prompt.Id,
        Label = prompt.Label,
        Response = prompt.Response,
        Hints = new PromptHints
        {
            ExpectedDataType = prompt.Hints.ExpectedDataType,
            Placeholder = prompt.Hints.Placeholder,
            HelpText = prompt.Hints.HelpText,
            ValidationPattern = prompt.Hints.ValidationPattern,
            SuggestedValues = new List<string>(prompt.Hints.SuggestedValues),
            ExprHidden = prompt.Hints.ExprHidden,
            ExprValue = prompt.Hints.ExprValue,
            ExprExpected = prompt.Hints.ExprExpected,
            ExprValidation = prompt.Hints.ExprValidation,
            ExprReadOnly = prompt.Hints.ExprReadOnly,
        },
    };
}
