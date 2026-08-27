using System.Text.Json;
using System.Text.Json.Serialization;
namespace PromptResponse.Core.Models;

/// <summary>The role a signature plays in an APR document.</summary>
public enum SignatureRole
{
    /// <summary>
    /// The publisher of the form attesting to the template definition (and the
    /// bound submission URL) — proves authenticity and that the form is unaltered.
    /// </summary>
    Publisher,

    /// <summary>
    /// A person who filled the form attesting to the responses in their scope.
    /// </summary>
    Filler,
}

/// <summary>
/// The identity of a signer, taken from their X.509 certificate. The authoritative
/// identity is the certificate embedded in <see cref="Signature.Cms"/>; these
/// fields are a convenient, human-readable projection of it.
/// </summary>
public class Signer
{
    /// <summary>
    /// Members present in the source JSON that this build does not recognise.
    /// </summary>
    /// <remarks>
    /// Captured on read and written back out unchanged, so a document produced by a
    /// newer minor version of the format survives a round-trip through this build
    /// instead of being silently stripped. This is what makes additive format change
    /// possible; see <see cref="AprFormat"/>.
    ///
    /// Not covered by signatures — the canonical payload enumerates known fields only,
    /// so extension members on a signed document can be altered without invalidating
    /// the signature. Not sanitised either: the text rules apply to known string fields.
    /// </remarks>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }

    /// <summary>Display name (the certificate subject's common name, or a provided label).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional stable identifier (email, org id, …).</summary>
    public string? Identifier { get; set; }

    /// <summary>The certificate subject distinguished name.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>The certificate issuer distinguished name (equal to the subject for self-signed).</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>The certificate's SHA-256 thumbprint (hex), for pinning/identification.</summary>
    public string Thumbprint { get; set; } = string.Empty;

    /// <summary>Whether the certificate is self-signed (issuer == subject).</summary>
    public bool SelfSigned { get; set; }
}

/// <summary>
/// A detached signature over part of an APR document (scheme <c>apr-sig-v2</c>):
/// an industry-standard CMS/PKCS#7 <c>SignedData</c> over the document's canonical
/// <em>content</em>, with the signer's X.509 certificate (chain) embedded. Pure
/// data; produced and verified by <c>PromptResponse.Core.Signing</c>.
/// </summary>
public class Signature
{
    /// <summary>
    /// Members present in the source JSON that this build does not recognise.
    /// </summary>
    /// <remarks>
    /// Captured on read and written back out unchanged, so a document produced by a
    /// newer minor version of the format survives a round-trip through this build
    /// instead of being silently stripped. This is what makes additive format change
    /// possible; see <see cref="AprFormat"/>.
    ///
    /// Not covered by signatures — the canonical payload enumerates known fields only,
    /// so extension members on a signed document can be altered without invalidating
    /// the signature. Not sanitised either: the text rules apply to known string fields.
    /// </remarks>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }

    /// <summary>A stable id for this signature (unique within the document).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Whether this is a publisher or a filler signature.</summary>
    public SignatureRole Role { get; set; }

    /// <summary>The signer's identity, projected from their certificate.</summary>
    public Signer Signer { get; set; } = new();

    /// <summary>
    /// What the signature covers: <c>"template"</c> (the whole form definition, for
    /// a publisher) or <c>"fields"</c> (the prompts listed in <see cref="Fields"/>).
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>The prompt ids this signature covers when <see cref="Scope"/> is <c>"fields"</c>.</summary>
    public List<string> Fields { get; set; } = new();


    /// <summary>The signature algorithm (e.g. <c>cms/ecdsa-p256-sha256</c>).</summary>
    public string Algorithm { get; set; } = "cms/ecdsa-p256-sha256";

    /// <summary>The canonical-payload scheme version.</summary>
    public string Canonicalization { get; set; } = "apr-sig-v2";

    /// <summary>When the signature was produced (ISO-8601 UTC), bound into the content.</summary>
    public string SignedAt { get; set; } = string.Empty;

    /// <summary>
    /// The base64-encoded detached CMS/PKCS#7 <c>SignedData</c> — the signature plus
    /// the signer's certificate chain.
    /// </summary>
    public string Cms { get; set; } = string.Empty;
}
