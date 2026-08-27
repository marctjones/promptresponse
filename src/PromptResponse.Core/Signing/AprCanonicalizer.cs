using System.Security.Cryptography;
using System.Text;
using PromptResponse.Core.Models;

namespace PromptResponse.Core.Signing;

/// <summary>
/// Produces the canonical byte payloads that APR signatures are computed over
/// (scheme <c>apr-sig-v1</c>). The format is deliberately simple and
/// language-portable — a fixed, ordered list of <c>label=base64(value)</c> lines —
/// rather than full JSON canonicalization, so other SDKs can reproduce it exactly
/// and signatures survive re-serialization.
/// </summary>
/// <remarks>
/// Two payload shapes:
/// <list type="bullet">
///   <item><b>Publisher</b> — template id/version, the bound submission URL, and a
///   digest of the <em>form definition</em> (structure/labels/hints, excluding
///   responses and the signatures array).</item>
///   <item><b>Filler</b> — template id/version and the sorted
///   <c>(fieldId, response)</c> pairs in the signer's scope.</item>
/// </list>
/// Both include the signer identity + public key + timestamp.
/// </remarks>
public static class AprCanonicalizer
{
    /// <summary>The canonical-payload scheme version.</summary>
    public const string Scheme = "apr-sig-v2";

    /// <summary>
    /// The bytes a publisher signs: form definition + the document's bound submission
    /// URL. The signer's identity is bound separately by the CMS certificate, so it is
    /// not part of this content payload.
    /// </summary>
    public static byte[] PublisherPayload(AprDocument document, string signedAt)
    {
        ArgumentNullException.ThrowIfNull(document);

        // The URL is read from the document, never from a caller-supplied copy or from
        // the signature object. Storing it twice was a real vulnerability: verification
        // recomputed from the signature's own copy, so redirecting
        // metadata.submissionUrl — the field a submitting client actually reads — left
        // the signature verifying as valid. One fact, one place.
        var submissionUrl = document.Metadata.SubmissionUrl;

        return new Writer()
            .Add("scheme", Scheme)
            .Add("role", "publisher")
            .Add("templateId", document.Metadata.TemplateId)
            .Add("templateVersion", document.Metadata.TemplateVersion)
            .Add("submissionUrl", submissionUrl)
            .Add("formDefDigest", Sha256Hex(FormDefinition(document)))
            .Add("signedAt", signedAt)
            .ToBytes();
    }

    /// <summary>The bytes a filler signs: the responses of the covered fields.</summary>
    public static byte[] FillerPayload(AprDocument document, IEnumerable<string> fields, string signedAt)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(fields);

        var responses = ResponsesById(document);
        var w = new Writer()
            .Add("scheme", Scheme)
            .Add("role", "filler")
            .Add("templateId", document.Metadata.TemplateId)
            .Add("templateVersion", document.Metadata.TemplateVersion);

        // Deterministic regardless of the order fields are listed in the signature.
        foreach (var id in fields.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
        {
            w.Add("field." + id, responses.GetValueOrDefault(id, string.Empty));
        }

        return w.Add("signedAt", signedAt).ToBytes();
    }

    /// <summary>
    /// The canonical bytes of the form definition: title/template ids and the
    /// ordered section/prompt structure with labels, hints, and table layout —
    /// but <em>not</em> responses, response metadata, or the signatures array.
    /// </summary>
    public static byte[] FormDefinition(AprDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var w = new Writer()
            .Add("scheme", Scheme + "/formdef")
            .Add("title", document.Metadata.Title)
            .Add("templateId", document.Metadata.TemplateId)
            .Add("templateVersion", document.Metadata.TemplateVersion);
        foreach (var section in document.Sections)
        {
            AppendSection(w, section);
        }
        return w.ToBytes();
    }

    private static void AppendSection(Writer w, Section s)
    {
        w.Add("S.id", s.Id).Add("S.title", s.Title).Add("S.desc", s.Description);

        // A table adds no separate column or row declarations to sign: its structure
        // IS the sections and prompts already covered below. Only the two facts that
        // are not otherwise represented are bound here.
        w.Add("S.kind", s.Kind).Add("S.canAddRows", s.CanAddRows).Add("S.maxRows", s.MaxRows);

        foreach (var p in s.Prompts)
        {
            AppendPrompt(w, p);
        }
        foreach (var child in s.Sections)
        {
            AppendSection(w, child);
        }
    }

    private static void AppendPrompt(Writer w, Prompt p)
    {
        var h = p.Hints;
        w.Add("P.id", p.Id)
         .Add("P.label", p.Label)
         .Add("P.type", h.ExpectedDataType)
         .Add("P.placeholder", h.Placeholder)
         .Add("P.help", h.HelpText)
         .Add("P.suggested", string.Join("", h.SuggestedValues))
         .Add("P.pattern", h.ValidationPattern)
         .Add("P.exprHidden", h.ExprHidden)
         .Add("P.exprValue", h.ExprValue)
         .Add("P.exprExpected", h.ExprExpected)
         .Add("P.exprValidation", h.ExprValidation)
         .Add("P.exprReadOnly", h.ExprReadOnly);
        // Intentionally excludes Response and ResponseMetadata: a publisher signs
        // the blank form, so a filler entering responses does not break it.
    }

    private static Dictionary<string, string> ResponsesById(AprDocument document)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var section in document.Sections)
        {
            CollectResponses(section, map);
        }
        return map;
    }

    private static void CollectResponses(Section s, Dictionary<string, string> into)
    {
        foreach (var p in s.Prompts)
        {
            if (!string.IsNullOrEmpty(p.Id))
            {
                into[p.Id] = p.Response;
            }
        }
        foreach (var child in s.Sections)
        {
            CollectResponses(child, into);
        }
    }

    private static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    /// <summary>
    /// Builds the canonical payload: ordered <c>label=base64(utf8(value))</c> lines.
    /// Base64-encoding the values makes the encoding unambiguous (no delimiter
    /// injection) and the line order is fixed by the caller.
    /// </summary>
    private sealed class Writer
    {
        private readonly StringBuilder _sb = new();

        public Writer Add(string label, string? value)
        {
            _sb.Append(label)
               .Append('=')
               .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty)))
               .Append('\n');
            return this;
        }

        public byte[] ToBytes() => Encoding.UTF8.GetBytes(_sb.ToString());
    }
}
