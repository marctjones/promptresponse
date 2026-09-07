using PromptResponse.Core;
using PromptResponse.Core.Models;
using PromptResponse.Core.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PromptResponse.Core.Serialization;

/// <summary>
/// JSON serializer for APR documents using System.Text.Json.
/// </summary>
/// <remarks>
/// Sanitizes every string field at the serialization boundary via
/// <see cref="StringSanitizer.NormalizeAndStrip"/>: NFC normalize so equal-looking
/// strings store as equal bytes, and strip the always-abusive character set
/// (BOM mid-string, bidi overrides, lone surrogates, control characters except
/// tab/LF/CR, non-character codepoints). Sanitization runs on both write AND read
/// so a tampered file fed in from outside is normalised before downstream code
/// sees it. Vision invariant preserved: legitimate Unicode (Persian ZWNJ, emoji
/// ZWJ sequences, bidi marks, combining accents) survives untouched.
/// </remarks>
public class AprJsonSerializer : IAprSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>Does this document spell the format version the way beta.6 retired?</summary>
    private static bool RetiredVersionMember(string content)
    {
        try
        {
            using var probe = JsonDocument.Parse(content);
            return probe.RootElement.ValueKind == JsonValueKind.Object
                && !probe.RootElement.TryGetProperty("aprVersion", out _)
                && probe.RootElement.TryGetProperty("version", out _);
        }
        catch (JsonException)
        {
            return false;   // Genuinely malformed; the shape message is the right one.
        }
    }

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
            },

            // Write back the members the document carried, and no others.
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { AprPresenceContract.OmitMembersTheDocumentDidNotCarry },
            }
        };
    }

    /// <inheritdoc />
    public string Serialize(AprDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        try
        {
            AprCompatibilityGuard.Validate(document);
            AprDocumentSanitizer.Sanitize(document);
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
            AprCompatibilityGuard.Validate(document);
            AprDocumentSanitizer.Sanitize(document);
            return document;
        }
        catch (JsonException ex)
        {
            // A document carrying the retired `version` member has no `aprVersion`, so
            // the required-member check fires first and reports the shape rather than the
            // cause. Say the cause: this is the commonest thing a pre-beta.6 document
            // does, and "Invalid JSON format" sends the reader looking for a syntax error
            // in a file whose syntax is fine.
            throw new SerializationException(
                RetiredVersionMember(content)
                    ? $"This document declares the retired `version` member. APR "
                      + $"{AprFormat.CurrentVersion} names it `aprVersion`, and a reader "
                      + "accepts no other spelling."
                    : "Invalid JSON format", ex);
        }
        catch (OperationCanceledException)
        {
            // Cancellation must propagate unchanged; wrapping it breaks async cancellation contracts.
            throw;
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
            AprCompatibilityGuard.Validate(document);
            AprDocumentSanitizer.Sanitize(document);
            return document;
        }
        catch (JsonException ex)
        {
            throw new SerializationException("Invalid JSON format", ex);
        }
        catch (OperationCanceledException)
        {
            // Cancellation must propagate unchanged; wrapping it breaks async cancellation contracts.
            throw;
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
            AprCompatibilityGuard.Validate(document);
            AprDocumentSanitizer.Sanitize(document);
            await JsonSerializer.SerializeAsync(stream, document, _options, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SerializationException("Failed to serialize APR document", ex);
        }
    }

}
