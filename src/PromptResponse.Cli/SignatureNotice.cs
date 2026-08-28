using PromptResponse.Core.Models;
using PromptResponse.Core.Signing;

namespace PromptResponse.Cli;

/// <summary>
/// What every command that opens a document says about its signatures.
/// </summary>
/// <remarks>
/// <para>
/// Written once so `info`, `validate`, `stats` and `review` cannot drift into telling
/// people different things about the same file. Before this, none of them mentioned
/// signatures at all - only `verify` did, which meant somebody had to already suspect a
/// problem in order to be told about one.
/// </para>
/// <para>
/// Three states, and only one of them warrants attention:
/// </para>
/// <list type="bullet">
///   <item><description><b>unsigned</b> - the ordinary case, and not suspicious. Signing
///     is optional and most documents are never signed. Treating "unsigned" as a warning
///     would make the common case look alarming and teach people to dismiss the message,
///     which disarms it for the case that matters.</description></item>
///   <item><description><b>signed and valid</b> - a claim somebody can check.</description></item>
///   <item><description><b>signed and broken</b> - somebody attested to this document and
///     it no longer matches what they attested to. Worth saying loudly.</description></item>
/// </list>
/// <para>
/// Reporting is never gating. Specification 6.1 is explicit that no validation error may
/// arise from the state of a signature, and that a validator rejecting a document because
/// a signature is missing or invalid is not implementing APR. So this prints; it never
/// changes an exit code on its own.
/// </para>
/// </remarks>
public static class SignatureNotice
{
    /// <summary>Prints what is worth saying about this document's signatures.</summary>
    /// <returns>True when at least one signature no longer verifies.</returns>
    public static bool Write(AprDocument document, TextWriter? output = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var write = output ?? Console.Out;

        if (document.Signatures is not { Count: > 0 })
        {
            return false;   // Unsigned. Not a finding, so nothing is said.
        }

        var results = AprVerifier.VerifyAll(document);
        var broken = results.Where(r => !r.ContentValid).ToList();

        write.WriteLine();
        if (broken.Count == 0)
        {
            write.WriteLine($"Signatures: {results.Count} present, all verify.");
            foreach (var r in results)
            {
                write.WriteLine($"  ok  {r.Role} - {r.SignerName} ({Describe(r.Trust)})");
            }
            return false;
        }

        write.WriteLine(
            $"⚠ Signatures: {broken.Count} of {results.Count} no longer verify.");
        foreach (var r in results)
        {
            write.WriteLine(r.ContentValid
                ? $"  ok     {r.Role} - {r.SignerName} ({Describe(r.Trust)})"
                : $"  BROKEN {r.Role} - {r.SignerName}: {r.Status}");
        }
        write.WriteLine(
            "  Somebody signed this document and it has changed since. The data is still");
        write.WriteLine(
            "  readable and still valid - a signature never withholds it (spec 9.5) - but");
        write.WriteLine(
            "  what they attested to is not what you are holding.");

        return true;
    }

    /// <summary>Trust in words, since the enum names alone tell a reader little.</summary>
    private static string Describe(SignatureTrust trust) => trust switch
    {
        SignatureTrust.Trusted => "trusted",
        SignatureTrust.SelfSigned => "self-signed, not pinned",
        SignatureTrust.Untrusted => "signer not trusted here",
        _ => trust.ToString().ToLowerInvariant(),
    };
}
