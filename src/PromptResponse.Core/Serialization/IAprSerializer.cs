using PromptResponse.Core.Models;

namespace PromptResponse.Core.Serialization;

/// <summary>
/// Interface for serializing and deserializing APR documents.
/// </summary>
public interface IAprSerializer
{
    /// <summary>
    /// Serializes an APR document to a string.
    /// </summary>
    /// <param name="document">The document to serialize.</param>
    /// <returns>A string representation of the document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when document is null.</exception>
    string Serialize(AprDocument document);

    /// <summary>
    /// Deserializes a string to an APR document.
    /// </summary>
    /// <param name="content">The serialized document content.</param>
    /// <returns>The deserialized APR document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when content is null.</exception>
    /// <exception cref="SerializationException">Thrown when deserialization fails.</exception>
    AprDocument Deserialize(string content);

    /// <summary>
    /// Asynchronously deserializes a stream to an APR document.
    /// </summary>
    /// <param name="stream">The stream containing the serialized document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized APR document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
    /// <exception cref="SerializationException">Thrown when deserialization fails.</exception>
    Task<AprDocument> DeserializeAsync(Stream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously serializes an APR document to a stream.
    /// </summary>
    /// <param name="document">The document to serialize.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentNullException">Thrown when document or stream is null.</exception>
    Task SerializeAsync(AprDocument document, Stream stream, CancellationToken cancellationToken = default);
}
