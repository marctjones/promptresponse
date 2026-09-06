using System.Text.Json;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Core.Beta6;

/// <summary>
/// Checks that every structural member carries the JSON type its member table declares.
/// </summary>
/// <remarks>
/// The specification splits two conditions that a typed deserializer runs together. A
/// <em>response</em> given as a number or a boolean is a parse failure, because a
/// response is always a string and a document that spells one as a number is not an APR
/// document at all (specification 7.3). A <em>structural member</em> of the wrong JSON
/// type is the validation error <c>WRONG_TYPE</c>, because the document parses: it is a
/// well-formed record that says something the format does not allow (specification 7.1).
///
/// System.Text.Json cannot tell them apart — both surface as a deserialization failure —
/// so twenty-six conformance cases were refused under the parse stage's generic code
/// when the format names a specific one. Checking the parsed value before the typed
/// model is built is what lets the right code be reported.
///
/// Only members the format types are listed. An unknown member is preserved and ignored
/// whatever it holds, which is what makes additive change to the format safe.
/// </remarks>
internal static class AprStructuralTypes
{
    private static readonly Dictionary<string, JsonValueKind[]> Document = new(StringComparer.Ordinal)
    {
        ["aprVersion"] = [JsonValueKind.String],
        ["documentType"] = [JsonValueKind.String],
        ["metadata"] = [JsonValueKind.Object],
        ["sections"] = [JsonValueKind.Array],
    };

    private static readonly Dictionary<string, JsonValueKind[]> Metadata = new(StringComparer.Ordinal)
    {
        ["title"] = [JsonValueKind.String],
        ["description"] = [JsonValueKind.String],
        ["author"] = [JsonValueKind.String],
        ["publisher"] = [JsonValueKind.String],
        ["templateId"] = [JsonValueKind.String],
        ["templateVersion"] = [JsonValueKind.String],
        ["created"] = [JsonValueKind.String],
        ["modified"] = [JsonValueKind.String],
        ["submissionUrls"] = [JsonValueKind.Array],
        ["regarding"] = [JsonValueKind.Array],
        ["roles"] = [JsonValueKind.Array],
    };

    private static readonly Dictionary<string, JsonValueKind[]> Section = new(StringComparer.Ordinal)
    {
        ["id"] = [JsonValueKind.String],
        ["title"] = [JsonValueKind.String],
        ["description"] = [JsonValueKind.String],
        ["role"] = [JsonValueKind.String],
        ["kind"] = [JsonValueKind.String],
        ["canAddRows"] = [JsonValueKind.True, JsonValueKind.False],
        ["maxRows"] = [JsonValueKind.Number],
        ["prompts"] = [JsonValueKind.Array],
        ["sections"] = [JsonValueKind.Array],
    };

    private static readonly Dictionary<string, JsonValueKind[]> Prompt = new(StringComparer.Ordinal)
    {
        ["id"] = [JsonValueKind.String],
        ["label"] = [JsonValueKind.String],
        // A response is always a string. A document spelling one as a number says
        // something the format does not allow, and it is refused rather than coerced:
        // "42" and 42 are different answers, and guessing which was meant is the one
        // thing this format will not do.
        ["response"] = [JsonValueKind.String],
        ["role"] = [JsonValueKind.String],
        ["hints"] = [JsonValueKind.Object],
    };

    private static readonly Dictionary<string, JsonValueKind[]> Hints = new(StringComparer.Ordinal)
    {
        ["expectedDataType"] = [JsonValueKind.String],
        ["placeholder"] = [JsonValueKind.String],
        ["helpText"] = [JsonValueKind.String],
        ["validationPattern"] = [JsonValueKind.String],
        ["suggestedValues"] = [JsonValueKind.Array],
        ["step"] = [JsonValueKind.Number],
        ["exprValue"] = [JsonValueKind.String],
        ["exprHidden"] = [JsonValueKind.String],
        ["exprReadOnly"] = [JsonValueKind.String],
        ["exprValidation"] = [JsonValueKind.String],
        // `min` and `max` are a number on an ordered numeric field and a canonical-form
        // string on a temporal one, so both spellings are the format's own.
        ["min"] = [JsonValueKind.Number, JsonValueKind.String],
        ["max"] = [JsonValueKind.Number, JsonValueKind.String],
    };

    /// <summary>Throws <c>WRONG_TYPE</c> for the first mistyped structural member.</summary>
    internal static void Require(JsonElement form)
    {
        Check(form, Document, "");
        if (form.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object)
        {
            Check(metadata, Metadata, "/metadata");
        }
        if (form.TryGetProperty("sections", out var sections) && sections.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var section in sections.EnumerateArray()) CheckSection(section, $"/sections/{index++}");
        }
    }

    private static void CheckSection(JsonElement section, string path)
    {
        if (section.ValueKind != JsonValueKind.Object) return;
        Check(section, Section, path);
        if (section.TryGetProperty("prompts", out var prompts) && prompts.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var prompt in prompts.EnumerateArray()) CheckPrompt(prompt, $"{path}/prompts/{index++}");
        }
        if (section.TryGetProperty("sections", out var nested) && nested.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var child in nested.EnumerateArray()) CheckSection(child, $"{path}/sections/{index++}");
        }
    }

    private static void CheckPrompt(JsonElement prompt, string path)
    {
        if (prompt.ValueKind != JsonValueKind.Object) return;
        Check(prompt, Prompt, path);
        if (prompt.TryGetProperty("hints", out var hints) && hints.ValueKind == JsonValueKind.Object)
        {
            Check(hints, Hints, $"{path}/hints");
        }
    }

    /// <summary>Members the format types as an array of strings.</summary>
    private static readonly HashSet<string> StringArrays = new(StringComparer.Ordinal)
    {
        "submissionUrls", "regarding", "suggestedValues",
    };

    private static void Check(JsonElement node, Dictionary<string, JsonValueKind[]> table, string path)
    {
        foreach (var member in node.EnumerateObject())
        {
            if (!table.TryGetValue(member.Name, out var allowed)) continue;
            // An explicit null is absence, not a wrong type: the member is simply not
            // there, and a member table's type governs a value that is present.
            if (member.Value.ValueKind == JsonValueKind.Null) continue;
            if (Array.IndexOf(allowed, member.Value.ValueKind) >= 0)
            {
                // An array member is typed by its elements too: "array of string" is a
                // type, and an array of numbers is not it.
                if (member.Value.ValueKind == JsonValueKind.Array && StringArrays.Contains(member.Name))
                {
                    var index = 0;
                    foreach (var element in member.Value.EnumerateArray())
                    {
                        if (element.ValueKind == JsonValueKind.String) { index++; continue; }
                        throw new SerializationException(
                            $"{path}/{member.Name}/{index} is {Spell(element.ValueKind)} where "
                            + "the format declares an array of strings.")
                        { Code = "WRONG_TYPE" };
                    }
                }
                continue;
            }
            throw new SerializationException(
                $"{path}/{member.Name} is {Spell(member.Value.ValueKind)} where the format "
                + $"declares {string.Join(" or ", allowed.Select(Spell))}.")
            { Code = "WRONG_TYPE" };
        }
    }

    private static string Spell(JsonValueKind kind) => kind switch
    {
        JsonValueKind.String => "a string",
        JsonValueKind.Number => "a number",
        JsonValueKind.True or JsonValueKind.False => "a boolean",
        JsonValueKind.Array => "an array",
        JsonValueKind.Object => "an object",
        JsonValueKind.Null => "null",
        _ => kind.ToString(),
    };
}
