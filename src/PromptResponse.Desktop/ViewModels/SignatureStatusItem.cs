namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// Display record for one signature's verification + trust status in the
/// signatures panel.
/// </summary>
/// <param name="Id">The signature id.</param>
/// <param name="Role">"Publisher" or "Filler".</param>
/// <param name="Signer">The signer's name (certificate subject).</param>
/// <param name="Scope">What the signature covers ("form definition" or a field list).</param>
/// <param name="ContentValid">Whether the covered content verifies (unaltered).</param>
/// <param name="Trust">Trust level ("Trusted", "SelfSigned", "Untrusted", "Invalid").</param>
/// <param name="Status">A human-readable status line.</param>
public sealed record SignatureStatusItem(
    string Id, string Role, string Signer, string Scope, bool ContentValid, string Trust, string Status)
{
    /// <summary>A short badge for the row: ✓ when content-valid, ✗ otherwise.</summary>
    public string Badge => ContentValid ? "✓" : "✗";

    /// <summary>Whether the signer's certificate is trusted (vs self-signed / untrusted).</summary>
    public bool IsTrusted => string.Equals(Trust, "Trusted", System.StringComparison.Ordinal);

    /// <summary>A compact header line for the row, e.g. "✓ Publisher — Town of Bloomfield".</summary>
    public string Headline => $"{Badge} {Role} — {Signer}";
}
