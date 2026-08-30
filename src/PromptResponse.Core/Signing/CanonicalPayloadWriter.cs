using System.Text;

namespace PromptResponse.Core.Signing;

/// <summary>Encodes ordered canonical payload lines without imposing payload semantics.</summary>
internal sealed class CanonicalPayloadWriter
{
    private readonly StringBuilder _content = new();

    internal CanonicalPayloadWriter Add(string label, string? value)
    {
        _content.Append(label)
            .Append('=')
            .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty)))
            .Append('\n');
        return this;
    }

    internal byte[] ToBytes() => Encoding.UTF8.GetBytes(_content.ToString());
}
