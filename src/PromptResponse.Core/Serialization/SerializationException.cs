namespace PromptResponse.Core.Serialization;

/// <summary>
/// Exception thrown when APR document serialization or deserialization fails.
/// </summary>
public class SerializationException : Exception
{
    /// <summary>The specification's code for this refusal, when one names it.</summary>
    /// <remarks>
    /// The parse stage has its own vocabulary, distinct from the validation error table
    /// (specification 7.3): a document that will not parse was never validated, so no
    /// validation error describes it. Carrying the code here means a caller reports what
    /// the format says rather than guessing from the message — which is how a reader
    /// ends up reporting <c>YAML_TAG_FORBIDDEN</c> for a directive, because "directive"
    /// contains neither word it was matching but the message mentioned a tag.
    ///
    /// Null where the specification names no code for the condition; a caller reports
    /// <c>PARSE_ERROR</c> then.
    /// </remarks>
    public string? Code { get; init; }

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
