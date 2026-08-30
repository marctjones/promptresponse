using PromptResponse.Core.Expressions;
using PromptResponse.Core.Models;
using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.ViewModels.Prompts.Presentation;

/// <summary>
/// Computes the small, accessible presentation contract for expression-derived answers.
/// Keeping this policy separate from the mutable prompt view model makes the two
/// provenance states easy to audit without coupling them to notification mechanics.
/// </summary>
internal static class PromptProvenancePresentation
{
    internal static bool IsComputed(Prompt prompt) =>
        !string.IsNullOrWhiteSpace(prompt.Hints?.ExprValue);

    internal static bool IsCalculated(Prompt prompt) =>
        IsComputed(prompt)
        && string.Equals(prompt.ResponseMetadata?.Source, FormExpressions.ComputedSource,
            StringComparison.Ordinal);

    internal static bool WasOverridden(Prompt prompt) =>
        IsComputed(prompt) && !IsCalculated(prompt) && !string.IsNullOrEmpty(prompt.Response);

    internal static string? Label(bool calculated, bool overridden) =>
        overridden ? "You changed this"
        : calculated ? "Calculated"
        : null;

    internal static string? Announcement(bool calculated, bool overridden, LiveRegionVerbosity verbosity)
    {
        if (!calculated && !overridden) return null;
        var terse = verbosity == LiveRegionVerbosity.Quiet;

        return overridden
            ? terse
                ? "You changed this from the calculated value."
                : "You changed this from the calculated value. The form will not " +
                  "calculate over it again."
            : terse
                ? "Calculated by the form."
                : "Calculated by the form. You can type over it if it is wrong.";
    }
}
