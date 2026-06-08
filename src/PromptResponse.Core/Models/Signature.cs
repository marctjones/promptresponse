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

/// <summary>The identity of a signer.</summary>
public class Signer
{
    /// <summary>Human-readable signer name (e.g. "Town of Bloomfield, CT", "Ada Lovelace").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional stable identifier (email, org id, DID, …).</summary>
    public string? Identifier { get; set; }

    /// <summary>
    /// The signer's public key as a PEM-encoded SubjectPublicKeyInfo block, used
    /// to verify <see cref="Signature.Value"/>. (An X.509 certificate chain is a
    /// planned additive alternative for PKI trust.)
    /// </summary>
    public string PublicKey { get; set; } = string.Empty;
}

/// <summary>
/// A detached cryptographic signature over part of an APR document. Pure data;
/// produced and verified by <c>PromptResponse.Core.Signing</c>.
/// </summary>
public class Signature
{
    /// <summary>A stable id for this signature (unique within the document).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Whether this is a publisher or a filler signature.</summary>
    public SignatureRole Role { get; set; }

    /// <summary>The signer's identity and public key.</summary>
    public Signer Signer { get; set; } = new();

    /// <summary>
    /// What the signature covers: <c>"template"</c> (the whole form definition, for
    /// a publisher) or <c>"fields"</c> (the prompts listed in <see cref="Fields"/>,
    /// for a filler).
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// The prompt ids this signature covers when <see cref="Scope"/> is
    /// <c>"fields"</c>. The signer attests to these prompts' current responses.
    /// </summary>
    public List<string> Fields { get; set; } = new();

    /// <summary>
    /// For a publisher signature, the submission URL bound into the signed payload
    /// (so it cannot be altered without invalidating the signature).
    /// </summary>
    public string? SubmissionUrl { get; set; }

    /// <summary>The signature algorithm (default <c>ecdsa-p256-sha256</c>).</summary>
    public string Algorithm { get; set; } = "ecdsa-p256-sha256";

    /// <summary>The canonical-payload scheme version (default <c>apr-sig-v1</c>).</summary>
    public string Canonicalization { get; set; } = "apr-sig-v1";

    /// <summary>When the signature was produced (ISO-8601 UTC).</summary>
    public string SignedAt { get; set; } = string.Empty;

    /// <summary>The base64-encoded signature bytes.</summary>
    public string Value { get; set; } = string.Empty;
}
