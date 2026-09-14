using System.Text.Json;

namespace PromptResponse.Core.Models;

/// <summary>
/// Independent copies of the prompt-tree models, member for member.
/// </summary>
/// <remarks>
/// An editor that copies a model by hand copies the members it remembers. What it
/// forgets is dropped in silence: unrecognised members above all, which a reader
/// holds for exactly one reason — a member present on read must still be present,
/// unchanged, on write (specification 5.8, APR-MODEL-021). A hand-written clone
/// that omits <see cref="Prompt.Extensions"/> turns an undo into data loss.
///
/// Reader state travels with the copy as well. <see cref="Prompt.Response"/> and
/// the collection properties record on assignment that the member was declared, so
/// a naive copy makes a prompt whose source said nothing about `response` start
/// claiming it said <c>""</c> — a different document, and a different digest.
///
/// This is the one place model-copy policy lives, so a member added to the models
/// has a single place to be accounted for. `ModelCopierCopiesEveryMember` in
/// PromptResponse.Core.Tests fails when a new member is not named here.
/// </remarks>
public static class ModelCopier
{
    /// <summary>Copies a section and, recursively, its prompts and child sections.</summary>
    public static Section Copy(Section section)
    {
        ArgumentNullException.ThrowIfNull(section);
        var copy = new Section
        {
            Extensions = CopyExtensions(section.Extensions),
            Id = section.Id,
            Title = section.Title,
            Description = section.Description,
            Kind = section.Kind,
            Role = section.Role,
            CanAddRows = section.CanAddRows,
            MaxRows = section.MaxRows,
            Prompts = section.Prompts.Select(Copy).ToList(),
            Sections = section.Sections.Select(Copy).ToList(),
        };
        copy.PromptsAreDeclared = section.PromptsAreDeclared;
        copy.SectionsAreDeclared = section.SectionsAreDeclared;
        return copy;
    }

    /// <summary>Copies a prompt, its hints, and its reader state.</summary>
    public static Prompt Copy(Prompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var copy = new Prompt
        {
            Extensions = CopyExtensions(prompt.Extensions),
            Id = prompt.Id,
            Label = prompt.Label,
            Role = prompt.Role,
            Hints = Copy(prompt.Hints),
        };
        // Not through the Response setter: assigning marks the response authored and
        // declared, which is a claim about the source document rather than the copy.
        copy.SetNormalizedResponse(prompt.Response);
        copy.ResponseIsDeclared = prompt.ResponseIsDeclared;
        copy.ComputedInThisSession = prompt.ComputedInThisSession;
        copy.HintsAreDeclared = prompt.HintsAreDeclared;
        return copy;
    }

    /// <summary>Copies a prompt's hints.</summary>
    public static PromptHints Copy(PromptHints hints)
    {
        ArgumentNullException.ThrowIfNull(hints);
        var copy = new PromptHints
        {
            Extensions = CopyExtensions(hints.Extensions),
            Placeholder = hints.Placeholder,
            ExpectedDataType = hints.ExpectedDataType,
            SuggestedValues = new List<string>(hints.SuggestedValues),
            HelpText = hints.HelpText,
            ValidationPattern = hints.ValidationPattern,
            Min = hints.Min,
            Max = hints.Max,
            Step = hints.Step,
            ExprHidden = hints.ExprHidden,
            ExprValue = hints.ExprValue,
            ExprExpected = hints.ExprExpected,
            ExprValidation = hints.ExprValidation,
            ExprReadOnly = hints.ExprReadOnly,
        };
        copy.SuggestedValuesAreDeclared = hints.SuggestedValuesAreDeclared;
        return copy;
    }

    /// <summary>Copies an extension-member bag, or returns null when there is none.</summary>
    /// <remarks>
    /// A new dictionary, so editing one object's extension members does not edit
    /// another's, keeping the source's comparer: member names are case-sensitive
    /// (specification 5.8) and a copy that folded case would merge two distinct
    /// members into one. The values are <see cref="JsonElement"/>, an immutable view
    /// over parsed JSON, so copying the entry is copying the member.
    /// </remarks>
    private static Dictionary<string, JsonElement>? CopyExtensions(
        Dictionary<string, JsonElement>? extensions)
        => extensions is null ? null : new Dictionary<string, JsonElement>(extensions, extensions.Comparer);
}
