using PromptResponse.Core.Models;

namespace PromptResponse.Core.Signing;

/// <summary>Whether a field's value is covered by anyone's signature, and whether that still holds.</summary>
public enum FieldSignatureState
{
    /// <summary>Nobody has signed this field's value.</summary>
    /// <remarks>
    /// The ordinary case, and not a suspicious one. Signing is optional (specification 9),
    /// and most documents are never signed at all.
    /// </remarks>
    Unsigned,

    /// <summary>At least one signature covers this value and still verifies.</summary>
    Signed,

    /// <summary>
    /// A signature covers this value, and no longer verifies.
    /// </summary>
    /// <remarks>
    /// The one state that warrants attention. It says somebody attested to this field and
    /// the document no longer matches what they attested to - which is a different and far
    /// more informative fact than "unsigned", and the reason a broken signature must never
    /// be quietly discarded.
    /// </remarks>
    Broken,
}

/// <summary>One signature's claim over a particular field.</summary>
/// <param name="SignatureId">The signature's id.</param>
/// <param name="SignerName">Who signed, from their certificate.</param>
/// <param name="ContentValid">Whether that signature still verifies over the document's current content.</param>
/// <param name="Trust">How much the signer's certificate is trusted.</param>
public sealed record CoveringSignature(
    string SignatureId, string SignerName, bool ContentValid, SignatureTrust Trust);

/// <summary>Which signatures cover which field values.</summary>
/// <remarks>
/// <para>
/// A document-level verdict - "Ada's signature is valid" - cannot answer the question
/// somebody has while looking at a particular field: is <em>this</em> value signed, by
/// whom, and does it still hold? On a form with a hundred fields and one signature over
/// twelve of them, the document-level answer is nearly useless.
/// </para>
/// <para>
/// Only filler signatures (<c>scope: "fields"</c>) cover a value. A publisher signature
/// covers the form definition - the questions, hints and structure - and deliberately
/// survives someone answering them (specification 9.3), so it says nothing about whether
/// an answer was attested to.
/// </para>
/// </remarks>
public static class SignatureCoverage
{
    /// <summary>Coverage for every field the document's signatures mention.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<CoveringSignature>> ForDocument(
        AprDocument document, AprTrustOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var coverage = new Dictionary<string, List<CoveringSignature>>(StringComparer.Ordinal);
        if (document.Signatures is not { Count: > 0 })
        {
            return coverage.ToDictionary(k => k.Key, v => (IReadOnlyList<CoveringSignature>)v.Value,
                StringComparer.Ordinal);
        }

        var verified = AprVerifier.VerifyAll(document, options)
            .ToDictionary(v => v.Id, StringComparer.Ordinal);

        foreach (var signature in document.Signatures)
        {
            if (signature.Role != SignatureRole.Filler || signature.Fields is not { Count: > 0 })
            {
                continue;
            }

            if (!verified.TryGetValue(signature.Id, out var result))
            {
                continue;
            }

            foreach (var fieldId in signature.Fields)
            {
                if (!coverage.TryGetValue(fieldId, out var list))
                {
                    coverage[fieldId] = list = [];
                }
                list.Add(new CoveringSignature(
                    signature.Id, result.SignerName, result.ContentValid, result.Trust));
            }
        }

        return coverage.ToDictionary(k => k.Key, v => (IReadOnlyList<CoveringSignature>)v.Value,
            StringComparer.Ordinal);
    }

    /// <summary>The state to report for a field, given who covers it.</summary>
    /// <remarks>
    /// One valid signature is enough to call a value signed. A second signer who has since
    /// been invalidated does not undo the first person's still-standing attestation - but
    /// the broken one is still worth reporting, which is why callers get the list too.
    /// </remarks>
    public static FieldSignatureState StateOf(IReadOnlyList<CoveringSignature>? covering) =>
        covering is not { Count: > 0 } ? FieldSignatureState.Unsigned
        : covering.Any(c => c.ContentValid) ? FieldSignatureState.Signed
        : FieldSignatureState.Broken;
}
