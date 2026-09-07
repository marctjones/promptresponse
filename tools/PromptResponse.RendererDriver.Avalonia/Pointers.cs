using System.Text.Json.Nodes;

namespace PromptResponse.RendererDriver.Avalonia;

/// <summary>Every section and prompt id, against the pointer that reaches it.</summary>
/// <remarks>
/// Ids are unique document-wide, so this says which control renders which member without
/// saying anything about how it was rendered. It is the same join the TypeScript driver
/// makes through `data-apr-prompt`, and the reason #369 put an AutomationId on every
/// section and prompt view.
/// </remarks>
internal static class Pointers
{
    internal static IReadOnlyDictionary<string, string> Of(JsonObject document)
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);

        void Walk(JsonArray? sections, string prefix)
        {
            for (var index = 0; index < (sections?.Count ?? 0); index++)
            {
                var section = sections![index]!.AsObject();
                var pointer = $"{prefix}/sections/{index}";
                Record(section, pointer);
                if (section["prompts"] is JsonArray prompts)
                    for (var position = 0; position < prompts.Count; position++)
                        Record(prompts[position]!.AsObject(), $"{pointer}/prompts/{position}");
                Walk(section["sections"] as JsonArray, pointer);
            }
        }

        void Record(JsonObject member, string pointer)
        {
            if (member["id"]?.GetValue<string>() is { Length: > 0 } id) found[id] = pointer;
        }

        Walk(document["sections"] as JsonArray, string.Empty);
        return found;
    }
}
