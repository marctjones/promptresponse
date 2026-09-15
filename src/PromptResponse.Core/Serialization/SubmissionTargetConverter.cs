using System.Text.Json;
using System.Text.Json.Serialization;
using PromptResponse.Core.Models;

namespace PromptResponse.Core.Serialization;

/// <summary>Reads and writes a <c>submissionUrls</c> entry in the spelling it arrived in.</summary>
public sealed class SubmissionTargetConverter : JsonConverter<SubmissionTarget>
{
    /// <summary>Reads a URL string as a <c>put</c> entry, and an object member by member.</summary>
    public override SubmissionTarget Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return SubmissionTarget.FromUrl(reader.GetString()!);
        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException(
                $"a submission entry is a string or an object; found {document.RootElement.ValueKind}");
        }
        var target = new SubmissionTarget();
        foreach (var member in document.RootElement.EnumerateObject())
        {
            switch (member.Name)
            {
                case "kind": target.Kind = Text(member); break;
                case "url": target.Url = Text(member); break;
                case "expires": target.Expires = Text(member); break;
                case "refresh": target.Refresh = Text(member); break;
                case "fields": target.Fields = member.Value.Clone(); break;
                default:
                    (target.Extensions ??= new(StringComparer.Ordinal))[member.Name] = member.Value.Clone();
                    break;
            }
        }
        return target;
    }

    /// <summary>Writes a string entry back as its string, and an object with only its own members.</summary>
    public override void Write(Utf8JsonWriter writer, SubmissionTarget value, JsonSerializerOptions options)
    {
        if (value.IsShorthand && value.Kind == SubmissionTarget.Put && value.Url is not null
            && value.Fields is null && value.Expires is null && value.Refresh is null
            && value.Extensions is not { Count: > 0 })
        {
            writer.WriteStringValue(value.Url);
            return;
        }
        writer.WriteStartObject();
        if (value.Kind is not null) writer.WriteString("kind", value.Kind);
        if (value.Url is not null) writer.WriteString("url", value.Url);
        if (value.Fields is { } fields)
        {
            writer.WritePropertyName("fields");
            fields.WriteTo(writer);
        }
        if (value.Expires is not null) writer.WriteString("expires", value.Expires);
        if (value.Refresh is not null) writer.WriteString("refresh", value.Refresh);
        foreach (var (name, member) in value.Extensions ?? [])
        {
            writer.WritePropertyName(name);
            member.WriteTo(writer);
        }
        writer.WriteEndObject();
    }

    private static string Text(JsonProperty member) =>
        member.Value.ValueKind == JsonValueKind.String
            ? member.Value.GetString()!
            : throw new JsonException($"a submission entry's {member.Name} is a string; found {member.Value.ValueKind}");
}
