using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using PromptResponse.Core.Models;

namespace PromptResponse.Core.Serialization;

/// <summary>Writes back the members a document carried, and no others.</summary>
/// <remarks>
/// A typed model cannot tell a member that was absent from one holding its default, so a
/// writer that regenerates from the model adds members nobody wrote: <c>documentType</c>,
/// an empty <c>sections</c>, an empty <c>response</c>, an empty <c>hints</c>. Each is
/// harmless in prose — absent means template, an absent response reads as the empty
/// string — and none of them is harmless to a digest. The specification computes it over
/// the parsed model and counts every member "that survived parsing" (APR-DIGEST-006,
/// APR-DIGEST-002), so a member that was not there and now is makes a different document.
/// Opening a signed form and saving it unchanged would invalidate every attestation over
/// it and break every <c>metadata.regarding</c> chain naming it.
///
/// Each model property carries a declared flag its setter sets, which is what
/// deserialization does when the member is present. The collections and hints also ask
/// whether they hold anything, because a list added to through <c>Add</c> was never
/// assigned and would otherwise be dropped.
/// </remarks>
internal static class AprPresenceContract
{
    internal static void OmitMembersTheDocumentDidNotCarry(JsonTypeInfo info)
    {
        if (info.Type == typeof(AprDocument))
            When(info, "documentType", owner => ((AprDocument)owner).DocumentTypeIsDeclared);
        else if (info.Type == typeof(Section))
        {
            When(info, "sections", owner => ((Section)owner) is { SectionsAreDeclared: true }
                or { Sections.Count: > 0 });
            When(info, "prompts", owner => ((Section)owner) is { PromptsAreDeclared: true }
                or { Prompts.Count: > 0 });
        }
        else if (info.Type == typeof(Prompt))
        {
            When(info, "response", owner => ((Prompt)owner) is { ResponseIsDeclared: true }
                or { Response.Length: > 0 });
            When(info, "hints", owner => ((Prompt)owner) is { HintsAreDeclared: true }
                or { Hints.IsEmpty: false });
        }
        else if (info.Type == typeof(PromptHints))
            When(info, "suggestedValues", owner => ((PromptHints)owner)
                is { SuggestedValuesAreDeclared: true } or { SuggestedValues.Count: > 0 });
    }

    /// <summary>Records which extension members each object carried when it was read.</summary>
    /// <remarks>
    /// Runs on deserialization only. The sanitizer runs when writing as well as reading,
    /// so recording there would mark a member set in code as arrived the first time
    /// anything serialized it, and the writer could no longer refuse it.
    /// </remarks>
    internal static void RecordExtensionsThatArrived(JsonTypeInfo info)
    {
        if (info.Type == typeof(AprDocument))
            info.OnDeserialized = owner => ((AprDocument)owner).ArrivedExtensionNames =
                NamesIn(((AprDocument)owner).Extensions);
        else if (info.Type == typeof(Metadata))
            info.OnDeserialized = owner => ((Metadata)owner).ArrivedExtensionNames =
                NamesIn(((Metadata)owner).Extensions);
        else if (info.Type == typeof(Section))
            info.OnDeserialized = owner => ((Section)owner).ArrivedExtensionNames =
                NamesIn(((Section)owner).Extensions);
        else if (info.Type == typeof(Prompt))
            info.OnDeserialized = owner => ((Prompt)owner).ArrivedExtensionNames =
                NamesIn(((Prompt)owner).Extensions);
        else if (info.Type == typeof(PromptHints))
            info.OnDeserialized = owner => ((PromptHints)owner).ArrivedExtensionNames =
                NamesIn(((PromptHints)owner).Extensions);
    }

    // Ordinal: member names are case-sensitive, so `Routing` did not arrive because
    // `routing` did.
    private static IReadOnlySet<string> NamesIn(Dictionary<string, JsonElement>? extensions) =>
        extensions is { Count: > 0 }
            ? extensions.Keys.ToFrozenSet(StringComparer.Ordinal)
            : FrozenSet<string>.Empty;

    private static void When(JsonTypeInfo info, string name, Func<object, bool> carried)
    {
        var property = info.Properties.FirstOrDefault(
            candidate => candidate.Name == name);
        if (property is null)
            throw new InvalidOperationException(
                $"{info.Type.Name} has no serialized member '{name}'. Renaming one without "
                + "updating this contract would silently start writing it again.");
        property.ShouldSerialize = (owner, _) => carried(owner);
    }
}
