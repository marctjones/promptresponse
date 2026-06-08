using System.Security.Cryptography.X509Certificates;
using PromptResponse.Core.Signing;

namespace PromptResponse.Cli.Commands;

/// <summary>
/// Generates a self-signed ECDSA P-256 signing certificate (PKCS#12 / .pfx), for
/// organizations or people who don't have a CA-issued certificate. Distribute the
/// public <c>.cer</c> so others can pin it as a trust anchor.
/// </summary>
public class KeygenCommand : ICommand
{
    public Task<int> ExecuteAsync(string[] args)
    {
        var name = GetArgValue(args, "--name");
        var output = GetArgValue(args, "--output");
        var password = GetArgValue(args, "--password");
        var certOut = GetArgValue(args, "--cert-out");
        var years = int.TryParse(GetArgValue(args, "--years"), out var y) ? y : 2;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(output))
        {
            Console.Error.WriteLine("Error: --name and --output are required");
            Console.Error.WriteLine("Usage: apr keygen --name=\"<signer>\" --output=<file.pfx> [--password=<pw>] [--cert-out=<file.cer>] [--years=<n>]");
            return Task.FromResult(1);
        }

        try
        {
            var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
            var notAfter = notBefore.AddYears(years);
            using var cert = SignatureCertificates.CreateSelfSigned(name!, notBefore, notAfter);

            File.WriteAllBytes(output!, cert.Export(X509ContentType.Pkcs12, password));
            Console.WriteLine($"Wrote signing certificate (private key): {output}");

            if (!string.IsNullOrWhiteSpace(certOut))
            {
                File.WriteAllBytes(certOut!, cert.Export(X509ContentType.Cert));
                Console.WriteLine($"Wrote public certificate (share to pin): {certOut}");
            }

            Console.WriteLine($"  Subject:    {cert.Subject}");
            Console.WriteLine($"  Thumbprint: {SignatureCertificates.Sha256Thumbprint(cert)}");
            Console.WriteLine($"  Valid:      {notBefore:yyyy-MM-dd} → {notAfter:yyyy-MM-dd}");
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: failed to generate certificate: {ex.Message}");
            return Task.FromResult(1);
        }
    }

    private static string? GetArgValue(string[] args, string prefix)
    {
        var arg = args.FirstOrDefault(a => a.StartsWith(prefix + "="));
        return arg?.Substring(prefix.Length + 1);
    }
}
