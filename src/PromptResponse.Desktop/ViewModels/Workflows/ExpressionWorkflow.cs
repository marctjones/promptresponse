using PromptResponse.Core.Expressions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.ViewModels.Workflows;

/// <summary>
/// Applies document expression hints to the prompt view models currently shown by
/// the desktop shell. The workflow owns the recompute-before-evaluate ordering and
/// re-entrancy guard; the shell retains response event wiring and presentation.
/// </summary>
internal sealed class ExpressionWorkflow
{
    private readonly Func<IEnumerable<PromptViewModelBase>> _prompts;

    public ExpressionWorkflow(Func<IEnumerable<PromptViewModelBase>> prompts)
    {
        _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
    }

    public bool IsApplying { get; private set; }

    public void Apply(AprDocument? document)
    {
        if (document is null || IsApplying) return;

        IsApplying = true;
        try
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            FormExpressions.RecomputeComputedValues(document, today);
            // Rebuild after computed values change so dependent visibility and
            // read-only expressions evaluate against the current model state.
            var expressions = FormExpressions.BuildContext(document, today);
            var promptsById = FormExpressions.GetAllPrompts(document)
                .Where(prompt => !string.IsNullOrEmpty(prompt.Id))
                .GroupBy(prompt => prompt.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

            foreach (var viewModel in _prompts())
            {
                if (!promptsById.TryGetValue(viewModel.Id, out var prompt)) continue;

                viewModel.IsVisible = !FormExpressions.IsHidden(prompt, expressions);
                viewModel.IsReadOnly = FormExpressions.IsReadOnly(prompt, expressions);
                viewModel.RefreshFromModel();
            }
        }
        finally
        {
            IsApplying = false;
        }
    }
}
