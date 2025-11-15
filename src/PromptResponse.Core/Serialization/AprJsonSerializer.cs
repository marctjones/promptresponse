using PromptResponse.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PromptResponse.Core.Serialization;

/// <summary>
/// JSON serializer for APR documents using System.Text.Json.
/// </summary>
public class AprJsonSerializer : IAprSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AprJsonSerializer"/> class.
    /// </summary>
    public AprJsonSerializer()
    {
        _options = new JsonSerializerOptions
        {
            // Use camelCase for JSON property names
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

            // Pretty-print the JSON for readability
            WriteIndented = true,

            // Ignore null values to keep the JSON clean
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

            // Convert enums to strings (e.g., "template" instead of 0)
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };
    }

    /// <inheritdoc />
    public string Serialize(AprDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        try
        {
            return JsonSerializer.Serialize(document, _options);
        }
        catch (Exception ex)
        {
            throw new SerializationException("Failed to serialize APR document", ex);
        }
    }

    /// <inheritdoc />
    public AprDocument Deserialize(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        try
        {
            var document = JsonSerializer.Deserialize<AprDocument>(content, _options);
            if (document == null)
            {
                throw new SerializationException("Deserialization returned null");
            }

            return document;
        }
        catch (JsonException ex)
        {
            throw new SerializationException("Invalid JSON format", ex);
        }
        catch (Exception ex) when (ex is not SerializationException)
        {
            throw new SerializationException("Failed to deserialize APR document", ex);
        }
    }

    /// <inheritdoc />
    public async Task<AprDocument> DeserializeAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            var document = await JsonSerializer.DeserializeAsync<AprDocument>(stream, _options, cancellationToken);
            if (document == null)
            {
                throw new SerializationException("Deserialization returned null");
            }

            return document;
        }
        catch (JsonException ex)
        {
            throw new SerializationException("Invalid JSON format", ex);
        }
        catch (Exception ex) when (ex is not SerializationException)
        {
            throw new SerializationException("Failed to deserialize APR document", ex);
        }
    }

    /// <inheritdoc />
    public async Task SerializeAsync(AprDocument document, Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            await JsonSerializer.SerializeAsync(stream, document, _options, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new SerializationException("Failed to serialize APR document", ex);
        }
    }
}
