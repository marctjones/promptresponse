using PromptResponse.Core.Models;

namespace PromptResponse.Core.Expressions;

/// <summary>Applies bounded computed-value settling without overwriting authored responses.</summary>
internal static class ComputedValueRecalculator
{
    internal static bool Recompute(
        AprDocument document, string? today, IReadOnlyDictionary<string, string>? ctx, string? now)
    {
        var changed = false;
        for (var pass = 0; pass < 5; pass++)
        {
            var context = FormExpressionContext.Create(document, today, ctx, now);
            var changedThisPass = false;
            foreach (var prompt in PromptTreeTraversal.GetAll(document))
            {
                if (string.IsNullOrWhiteSpace(prompt.Hints?.ExprValue)) continue;
                // Every non-empty response in the document as it was read is authored,
                // whatever produced it. What this session computed is tracked in
                // ComputedInThisSession, and the document records nothing about it.
                if (!string.IsNullOrEmpty(prompt.Response) && !prompt.ComputedInThisSession) continue;
                var computed = FormExpressions.ComputeValue(prompt, context);
                if (computed is null || string.Equals(computed, prompt.Response, StringComparison.Ordinal)) continue;
                prompt.Response = computed;
                prompt.ComputedInThisSession = true;
                changedThisPass = true;
            }
            changed |= changedThisPass;
            if (!changedThisPass) break;
        }
        return changed;
    }
}
