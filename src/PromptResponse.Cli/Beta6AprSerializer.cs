using System.Text;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Cli;

/// <summary>CLI document serializer restricted to one beta.6 JSONC form.</summary>
internal sealed class Beta6AprSerializer : IAprSerializer
{
    private readonly AprBeta6Reader _reader = new();

    public string Serialize(AprDocument document) => _reader.WriteForm(document, AprRepresentation.Jsonc);

    public AprDocument Deserialize(string content) => _reader.ReadForm(content, AprRepresentation.Jsonc);

    public async Task<AprDocument> DeserializeAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return Deserialize(await reader.ReadToEndAsync(cancellationToken));
    }

    public async Task SerializeAsync(AprDocument document, Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        await writer.WriteAsync(Serialize(document).AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
    }
}
