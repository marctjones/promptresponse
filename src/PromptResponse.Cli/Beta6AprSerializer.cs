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
/// the requested output path's extension). Reading tolerates both: nothing here
/// is told which representation a file is in, so <see cref="Deserialize"/> tries
/// JSONC first and falls back to YAML, which is what let every command built on
/// this serializer read only JSONC until 2026-09-07 — a `.apr.yaml` file crashed
/// with a JSON parse error before reaching anything that could say otherwise.
/// </summary>
internal sealed class Beta6AprSerializer : IAprSerializer
{
    private readonly AprBeta6Reader _reader = new();

    public string Serialize(AprDocument document) => _reader.WriteForm(document, AprRepresentation.Jsonc);

    public AprDocument Deserialize(string content)
    {
        try
        {
            return _reader.ReadForm(content, AprRepresentation.Jsonc);
        }
        // AprStreamRequiresIterationException (a SerializationException) means this
        // parsed as valid JSONC and turned out to hold more than one record — that is
        // a real answer, not a wrong-representation guess, and must propagate rather
        // than triggering a YAML retry.
        catch (SerializationException ex)
            when (ex is not AprStreamRequiresIterationException && LooksLikeYaml(content))
        {
            return _reader.ReadForm(content, AprRepresentation.Yaml);
        }
    }

    /// <summary>
    /// A beta.6 JSONC form is always a JSON object, so it always starts with '{'
    /// once whitespace, a BOM, and JSONC comments are skipped. Anything else that
    /// failed as JSONC is worth one retry as YAML rather than surfacing a JSON
    /// parse error for a file that was never JSON to begin with.
    /// </summary>
    private static bool LooksLikeYaml(string content)
    {
        var index = 0;
        if (content.Length > 0 && content[0] == '﻿') index = 1;
        while (index < content.Length)
        {
            var ch = content[index];
            if (char.IsWhiteSpace(ch)) { index++; continue; }
            if (ch == '/' && index + 1 < content.Length && content[index + 1] is '/' or '*')
            {
                // Skip a JSONC comment rather than mistaking it for content.
                var lineComment = content[index + 1] == '/';
                var end = lineComment
                    ? content.IndexOf('\n', index + 2)
                    : content.IndexOf("*/", index + 2, StringComparison.Ordinal);
                if (end < 0) return true; // an unterminated comment is not valid JSONC either
                index = lineComment ? end + 1 : end + 2;
                continue;
            }
            return ch != '{';
        }
        return true; // empty or all-comment content is not valid JSONC either
    }

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
