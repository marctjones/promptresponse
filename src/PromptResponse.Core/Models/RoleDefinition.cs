using System.Text.Json;
using System.Text.Json.Serialization;

namespace PromptResponse.Core.Models;

/// <summary>
/// A party the form's author expects to fill part of it.
/// </summary>
/// <remarks>
/// <para>
/// Section and prompt <c>role</c> members carry an identifier - "nurse", "office" - and
/// an identifier is not something to show a person. Declaring roles here gives each one a
/// name and, where it helps, a sentence saying who is meant: a reader asking "which role
/// are you filling?" can then offer <i>Nurse — clinical staff recording observations</i>
/// rather than the bare slug <c>nurse</c>.
/// </para>
/// <para>
/// Declaring is optional and referencing an undeclared role stays valid, because the role
/// vocabulary is open (specification 4.10). A reader meeting an undeclared role shows the
/// identifier and carries on; a validator may warn, and never errors.
/// </para>
/// </remarks>
public class RoleDefinition
{
    /// <summary>Unknown members are preserved on round-trip (specification 4.8).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }

    /// <summary>The identifier that section and prompt <c>role</c> members reference.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The name to show a person. Falls back to <see cref="Id"/> when absent.</summary>
    public string? Name { get; set; }

    /// <summary>Who this role is, in a sentence, when the name alone is not obvious.</summary>
    public string? Description { get; set; }

    /// <summary>The name to display: <see cref="Name"/> if given, otherwise the identifier.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;
}
