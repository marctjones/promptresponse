using PromptResponse.Core.Signing;
using PromptResponse.Desktop.Profiles;

namespace PromptResponse.Desktop.ViewModels.Prompts.Presentation;

/// <summary>Pure presentation policy for per-field signature coverage.</summary>
internal static class PromptSignaturePresentation
{
    internal static string? Label(IReadOnlyList<CoveringSignature> covering)
    {
        var state = SignatureCoverage.StateOf(covering);
        return state switch
        {
            FieldSignatureState.Signed when covering.Count(c => c.ContentValid) > 1 =>
                $"Signed by {covering.Count(c => c.ContentValid)} people",
            FieldSignatureState.Signed => $"Signed by {SignerNames(covering, valid: true)}",
            FieldSignatureState.Broken => "Signature broken",
            _ => null,
        };
    }

    internal static string? Announcement(IReadOnlyList<CoveringSignature> covering, LiveRegionVerbosity verbosity)
    {
        var state = SignatureCoverage.StateOf(covering);
        if (state == FieldSignatureState.Unsigned) return null;
        var terse = verbosity == LiveRegionVerbosity.Quiet;

        return state switch
        {
            FieldSignatureState.Broken when terse => "Signature broken.",
            FieldSignatureState.Broken =>
                $"Signed by {SignerNames(covering, valid: false)}, but this answer has changed since. " +
                "Their signature no longer verifies. You can still edit this field.",
            _ when terse => $"Signed by {SignerNames(covering, valid: true)}.",
            _ => $"Signed by {SignerNames(covering, valid: true)}. Editing it will break their signature.",
        };
    }

    private static string SignerNames(IReadOnlyList<CoveringSignature> covering, bool valid)
    {
        var names = covering.Where(c => c.ContentValid == valid)
            .Select(c => c.SignerName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return names.Count switch
        {
            0 => "someone",
            1 => names[0],
            2 => $"{names[0]} and {names[1]}",
            _ => $"{names[0]} and {names.Count - 1} others",
        };
    }
}
