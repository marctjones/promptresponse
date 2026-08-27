using PromptResponse.Core;
using PromptResponse.Core.Models;
using PromptResponse.Core.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            SanitizeDocument(document);
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
            SanitizeDocument(document);
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
            SanitizeDocument(document);
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
            SanitizeDocument(document);
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

    /// <summary>Walks the document tree and applies <see cref="StringSanitizer.NormalizeAndStrip"/>
    /// to every user-content string. Field-name strings (Id, hint type names) are
    /// left untouched — they're internal identifiers, not user-typed responses.</summary>
    private static void SanitizeDocument(AprDocument document)
    {
        AprFormat.DropRetiredMembers(document.Extensions);
        if (document.Metadata != null)
        {
            AprFormat.DropRetiredMembers(document.Metadata.Extensions);
            document.Metadata.Title = StringSanitizer.NormalizeAndStrip(document.Metadata.Title) ?? string.Empty;
            document.Metadata.Description = StringSanitizer.NormalizeAndStrip(document.Metadata.Description);
            document.Metadata.Author = StringSanitizer.NormalizeAndStrip(document.Metadata.Author);
            document.Metadata.FilledBy = StringSanitizer.NormalizeAndStrip(document.Metadata.FilledBy);
            document.Metadata.Publisher = StringSanitizer.NormalizeAndStrip(document.Metadata.Publisher);
            // SubmissionUrl is deliberately NOT rewritten. It is machine-consumed and
            // signature-bound, so a hidden character in it is reported and blocks
            // signing rather than being quietly cleaned to some other host.
        }
        foreach (var section in document.Sections)
        {
            SanitizeSection(section);
        }
    }

    private static void SanitizeSection(Section section)
    {
        AprFormat.DropRetiredMembers(section.Extensions);
        section.Title = StringSanitizer.NormalizeAndStrip(section.Title) ?? string.Empty;
        section.Description = StringSanitizer.NormalizeAndStrip(section.Description);
        foreach (var prompt in section.Prompts)
        {
            SanitizePrompt(prompt);
        }
        foreach (var nested in section.Sections)
        {
            SanitizeSection(nested);
        }
    }

    private static void SanitizePrompt(Prompt prompt)
    {
        AprFormat.DropRetiredMembers(prompt.Extensions);
        AprFormat.DropRetiredMembers(prompt.Hints?.Extensions);
        AprFormat.DropRetiredMembers(prompt.ResponseMetadata?.Extensions);
        prompt.Label = StringSanitizer.NormalizeAndStrip(prompt.Label) ?? string.Empty;
        // A response is filled data: it is never altered on the basis of a hint. The
        // author's choice of expectedDataType says what they hoped to receive; it does
        // not license editing what someone actually wrote. Suspicious characters are
        // reported by HiddenCharacterAdvisor and left in place.
        prompt.Response = StringSanitizer.NormalizeAndStrip(prompt.Response) ?? string.Empty;
        if (prompt.Hints != null)
        {
            prompt.Hints.HelpText = StringSanitizer.NormalizeAndStrip(prompt.Hints.HelpText);
            prompt.Hints.Placeholder = StringSanitizer.NormalizeAndStrip(prompt.Hints.Placeholder);
        }
    }
}
