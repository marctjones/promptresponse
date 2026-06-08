using System.Security.Cryptography.X509Certificates;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Signing;

namespace PromptResponse.Cli.Commands;

/// <summary>
/// Verifies the signatures on an APR document: whether each one's covered content
/// is unchanged, and how far the signer's certificate is trusted (chains to a
/// configured trust anchor, or is a pinned self-signed cert).
/// </summary>
public class VerifyCommand : ICommand
{
    private readonly IAprSerializer _serializer;

    public VerifyCommand(IAprSerializer serializer) => _serializer = serializer;

    public async Task<int> ExecuteAsync(string[] args)
    {
        var filePath = args.FirstOrDefault(a => !a.StartsWith("--"));
        var trustArg = GetArgValue(args, "--trust");
        var checkRevocation = args.Contains("--check-revocation");

        if (string.IsNullOrEmpty(filePath))
        {
            Console.Error.WriteLine("Error: File path required");
            Console.Error.WriteLine("Usage: apr verify <file> [--trust=<anchor1.cer,anchor2.cer>] [--check-revocation]");
            return 1;
        }
        if (!File.Exists(filePath)) { Console.Error.WriteLine($"Error: File not found: {filePath}"); return 1; }

        try
        {
            var document = _serializer.Deserialize(await File.ReadAllTextAsync(filePath));

            List<X509Certificate2>? anchors = null;
            if (!string.IsNullOrEmpty(trustArg))
            {
                anchors = new List<X509Certificate2>();
                foreach (var p in trustArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!File.Exists(p)) { Console.Error.WriteLine($"Error: trust anchor not found: {p}"); return 1; }
                    anchors.Add(SignatureCertificates.LoadCertificate(p));
                }
            }

            var options = new AprTrustOptions { TrustAnchors = anchors, CheckRevocation = checkRevocation };
            var results = AprVerifier.VerifyAll(document, options);

            if (results.Count == 0)
            {
                Console.WriteLine("No signatures on this document.");
                return 0;
            }

            Console.WriteLine($"Signatures: {results.Count}");
            foreach (var r in results)
            {
                var mark = !r.ContentValid ? "✗ INVALID"
                    : r.Trust == SignatureTrust.Trusted ? "✓ trusted"
                    : r.Trust == SignatureTrust.SelfSigned ? "~ self-signed"
                    : "! untrusted";
                Console.WriteLine($"  [{r.Role}] {r.Id}: {mark}");
                Console.WriteLine($"      signer: {r.SignerName}");
                Console.WriteLine($"      status: {r.Status}");
            }

            var anyInvalid = results.Any(r => !r.ContentValid);
            Console.WriteLine(anyInvalid
                ? "Result: ✗ one or more signatures are INVALID (content was altered)."
                : "Result: ✓ all signatures verify over the current content.");
            return anyInvalid ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: failed to verify: {ex.Message}");
            return 1;
        }
    }

    private static string? GetArgValue(string[] args, string prefix)
    {
        var arg = args.FirstOrDefault(a => a.StartsWith(prefix + "="));
        return arg?.Substring(prefix.Length + 1);
    }
}
