using System.Text.Json;
using System.Text.Json.Serialization;

namespace PromptResponse.Core.Serialization;

/// <summary>Reads and writes a member the format types as "number or string".</summary>
/// <remarks>
/// `min` and `max` are a number on an ordered numeric field and a canonical-form string
/// on a date, time or datetime one, because a bound has to be comparable in the space the
/// field lives in. Both spellings are the format's, so neither may be rewritten into the
/// other: writing 5 back as "5" would change the document's semantic model and therefore
/// its digest.
/// </remarks>
public sealed class NumberOrStringConverter : JsonConverter<object?>
{
    /// <summary>Reads the member, keeping the spelling the document used.</summary>
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number when reader.TryGetInt64(out var whole) => whole,
            JsonTokenType.Number => reader.GetDouble(),
            _ => throw new JsonException(
                $"min and max are a number or a string; found {reader.TokenType}"),
        };

    /// <summary>Writes the member back in the spelling it was read as.</summary>
    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case null: writer.WriteNullValue(); break;
            case string text: writer.WriteStringValue(text); break;
            case long whole: writer.WriteNumberValue(whole); break;
            case int whole: writer.WriteNumberValue(whole); break;
            case double number: writer.WriteNumberValue(number); break;
            case decimal number: writer.WriteNumberValue(number); break;
            default: writer.WriteStringValue(value.ToString()); break;
        }
    }
}
