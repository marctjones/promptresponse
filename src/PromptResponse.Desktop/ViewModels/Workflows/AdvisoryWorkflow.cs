using System.Collections.ObjectModel;
using PromptResponse.Core.Expressions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Validation;

namespace PromptResponse.Desktop.ViewModels.Workflows;

/// <summary>
/// Computes the non-blocking advisories shown by the desktop shell.
/// Keeping this policy outside the shell makes the validation sources and their
/// prompt-linking behavior independently testable while the shell remains the
/// owner of presentation bindings.
/// </summary>
internal sealed class AdvisoryWorkflow
{
    private readonly DataTypeValidator _dataTypeValidator = new();
    private readonly HiddenCharacterAdvisor _hiddenCharacterAdvisor = new();
    private readonly MixedScriptAdvisor _mixedScriptAdvisor = new();
    private readonly ObservableCollection<AdvisoryItem> _items = new();

    public event Action? StateChanged;

    public IReadOnlyList<AdvisoryItem> Items => _items;

    public int Count => _items.Count;

    public void Refresh(AprDocument? document)
    {
        _items.Clear();
        if (document is not null)
        {
            AddValidatorWarnings(document, _dataTypeValidator.ValidateDocument(document).Warnings);
            AddValidatorWarnings(document, _hiddenCharacterAdvisor.Validate(document).Warnings);
            AddValidatorWarnings(document, _mixedScriptAdvisor.Validate(document).Warnings);
            AddExpressionWarnings(document);
        }

        StateChanged?.Invoke();
    }

    private void AddValidatorWarnings(AprDocument document, IEnumerable<ValidationWarning> warnings)
    {
        foreach (var warning in warnings)
        {
            var (promptId, promptLabel) = ResolvePrompt(document, warning.PropertyPath);
            _items.Add(new AdvisoryItem(promptId, promptLabel, warning.Message));
        }
    }

    private void AddExpressionWarnings(AprDocument document)
    {
        var expressions = FormExpressions.BuildContext(document, DateTime.UtcNow.ToString("yyyy-MM-dd"));
        foreach (var prompt in FormExpressions.GetAllPrompts(document))
        {
            var message = FormExpressions.Validate(prompt, expressions);
            if (message is not null)
            {
                _items.Add(new AdvisoryItem(prompt.Id, prompt.Label, message));
            }
        }
    }

    private static (string id, string label) ResolvePrompt(AprDocument document, string propertyPath)
    {
        var prompt = FindPromptById(document.Sections, propertyPath);
        return prompt is not null ? (prompt.Id, prompt.Label) : (propertyPath, propertyPath);
    }

    private static Prompt? FindPromptById(IList<Section> sections, string id)
    {
        foreach (var section in sections)
        {
            foreach (var prompt in section.Prompts)
            {
                if (prompt.Id == id) return prompt;
            }

            var nested = FindPromptById(section.Sections, id);
            if (nested is not null) return nested;
        }

        return null;
    }
}
