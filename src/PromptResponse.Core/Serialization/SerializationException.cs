namespace PromptResponse.Core.Serialization;

/// <summary>
/// Exception thrown when APR document serialization or deserialization fails.
/// </summary>
public class SerializationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SerializationException"/> class.
    /// </summary>
    public SerializationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SerializationException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public SerializationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SerializationException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public SerializationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
