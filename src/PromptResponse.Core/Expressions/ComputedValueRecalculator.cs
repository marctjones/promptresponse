using PromptResponse.Core.Models;

namespace PromptResponse.Core.Expressions;

/// <summary>Applies bounded computed-value settling without overwriting authored responses.</summary>
internal static class ComputedValueRecalculator
{
    internal static bool Recompute(AprDocument document, string? today, IReadOnlyDictionary<string, string>? ctx)
    {
        var changed = false;
        for (var pass = 0; pass < 5; pass++)
        {
            var context = FormExpressionContext.Create(document, today, ctx);
            var changedThisPass = false;
            foreach (var prompt in PromptTreeTraversal.GetAll(document))
            {
                if (string.IsNullOrWhiteSpace(prompt.Hints?.ExprValue)) continue;
                var authored = !string.IsNullOrEmpty(prompt.Response) && !string.Equals(prompt.ResponseMetadata?.Source, FormExpressions.ComputedSource, StringComparison.Ordinal);
                if (authored) continue;
                var computed = FormExpressions.ComputeValue(prompt, context);
                if (computed is null || string.Equals(computed, prompt.Response, StringComparison.Ordinal)) continue;
                prompt.Response = computed;
                prompt.ResponseMetadata ??= new ResponseMetadata();
                prompt.ResponseMetadata.Source = FormExpressions.ComputedSource;
                changedThisPass = true;
            }
            changed |= changedThisPass;
            if (!changedThisPass) break;
        }
        return changed;
    }
}
