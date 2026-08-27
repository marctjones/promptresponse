namespace PromptResponse.Core.Models;

/// <summary>
/// Who each part of a form is meant for.
/// </summary>
/// <remarks>
/// <para>
/// Most real forms are filled by more than one person. A patient completes the intake, a
/// nurse records observations, the office stamps a reference number. Without somewhere to
/// say so, all three appear as one undifferentiated list of questions and the patient is
/// left guessing which ones are theirs.
/// </para>
/// <para>
/// A role says who a field is <i>for</i>. It never says who may type into it. The format
/// has no identity at fill time - nothing in a JSON document knows who is at the keyboard -
/// so a reader marks a field as somebody else's and still accepts input, exactly as it
/// does for every other hint. Any string remains a valid response.
/// </para>
/// <para>
/// Accountability arrives afterwards and from a different mechanism: a scoped filler
/// signature over those fields, made with the nurse's certificate, is evidence the nurse
/// filled them (specification 9.3). A greyed-out box is evidence of nothing at all, which
/// is why the format declines to pretend otherwise.
/// </para>
/// </remarks>
public static class FormRoles
{
    /// <summary>
    /// The role in force for a prompt: its own, or the nearest enclosing section's.
    /// </summary>
    /// <param name="prompt">The prompt.</param>
    /// <param name="enclosing">
    /// The sections containing it, outermost first. Only the innermost role that is set
    /// applies, so a single field inside "For office use" can be handed back to the
    /// patient without splitting the section in two.
    /// </param>
    public static string? Effective(Prompt prompt, IEnumerable<Section> enclosing)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(enclosing);

        if (!string.IsNullOrWhiteSpace(prompt.Role))
        {
            return prompt.Role;
        }

        string? inherited = null;
        foreach (var section in enclosing)
        {
            if (!string.IsNullOrWhiteSpace(section.Role))
            {
                inherited = section.Role;
            }
        }
        return inherited;
    }

    /// <summary>Every prompt in the document paired with the role in force for it.</summary>
    public static IEnumerable<(Prompt Prompt, string? Role)> Resolve(AprDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        IEnumerable<(Prompt, string?)> Walk(Section section, string? inherited)
        {
            var here = string.IsNullOrWhiteSpace(section.Role) ? inherited : section.Role;
            foreach (var prompt in section.Prompts)
            {
                yield return (prompt, string.IsNullOrWhiteSpace(prompt.Role) ? here : prompt.Role);
            }
            foreach (var child in section.Sections)
            {
                foreach (var found in Walk(child, here))
                {
                    yield return found;
                }
            }
        }

        return document.Sections.SelectMany(s => Walk(s, null));
    }

    /// <summary>Every distinct role the document assigns, in document order.</summary>
    /// <remarks>What the form <i>uses</i>, which is not necessarily what it declares.</remarks>
    public static IReadOnlyList<string> Used(AprDocument document) =>
        Resolve(document)
            .Select(r => r.Role)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>The declaration for a role identifier, or null when it was never declared.</summary>
    /// <remarks>
    /// Undeclared is not an error. The vocabulary is open, so a reader that finds nothing
    /// here shows the identifier itself rather than refusing the document.
    /// </remarks>
    public static RoleDefinition? Definition(AprDocument document, string? role)
    {
        ArgumentNullException.ThrowIfNull(document);
        return string.IsNullOrWhiteSpace(role)
            ? null
            : document.Roles?.FirstOrDefault(r => string.Equals(r.Id, role, StringComparison.Ordinal));
    }

    /// <summary>The name to show for a role: its declared name, else the identifier itself.</summary>
    public static string? DisplayName(AprDocument document, string? role) =>
        string.IsNullOrWhiteSpace(role) ? null : Definition(document, role)?.DisplayName ?? role;

    /// <summary>
    /// Roles the document uses but never declares.
    /// </summary>
    /// <remarks>
    /// Advisory. An author who assigns a section to "nurse" and forgets to declare it has
    /// produced a valid form that shows a bare identifier where a name should be - worth
    /// a warning at authoring time (specification 6.2), never an error.
    /// </remarks>
    public static IReadOnlyList<string> Undeclared(AprDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var declared = document.Roles?.Select(r => r.Id).ToHashSet(StringComparer.Ordinal)
                       ?? new HashSet<string>(StringComparer.Ordinal);
        return Used(document).Where(r => !declared.Contains(r)).ToList();
    }
}
