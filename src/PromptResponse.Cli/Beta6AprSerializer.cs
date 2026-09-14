using System.Text;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;

namespace PromptResponse.Cli;

/// <summary>
/// CLI document serializer. Writes one beta.6 form, always as JSONC — the
/// <see cref="IAprSerializer"/> contract has no parameter for choosing a
/// representation, so a caller that needs APR-YAML output writes it directly
/// through <see cref="AprBeta6Reader.WriteForm"/> instead (see
/// <c>FilledFormWriter</c> and <c>NewCommand</c>, which pick a representation from
/// the requested output path's extension). Reading takes the representation from the
/// content, as every reader does ([Document type](#media-types)).
/// </summary>
internal sealed class Beta6AprSerializer : IAprSerializer
{
    private readonly AprBeta6Reader _reader = new();

    public string Serialize(AprDocument document) => _reader.WriteForm(document, AprRepresentation.Jsonc);

    public AprDocument Deserialize(string content) =>
        _reader.ReadForm(content, AprBeta6Reader.RepresentationOf(content));

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
