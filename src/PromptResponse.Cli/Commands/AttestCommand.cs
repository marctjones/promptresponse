using System.Security.Cryptography.X509Certificates;
using PromptResponse.Core.Beta6;

namespace PromptResponse.Cli.Commands;

/// <summary>Appends a detached beta.6 CMS attestation without mutating its subject form.</summary>
public sealed class AttestCommand : ICommand
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: apr attest <stream> --cert=<certificate.pfx> --output=<stream> [--password=<password>] [--fields=<id,id>]");
            return 1;
        }
        var input = args[0];
        var certificatePath = Option(args, "--cert=");
        var output = Option(args, "--output=");
        if (!File.Exists(input) || string.IsNullOrWhiteSpace(certificatePath) || !File.Exists(certificatePath) || string.IsNullOrWhiteSpace(output))
        {
            Console.Error.WriteLine("Error: attest requires an existing input stream, --cert=<certificate.pfx>, and --output=<stream>.");
            return 1;
        }

        try
        {
            var representation = input.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || input.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                ? AprRepresentation.Yaml : AprRepresentation.Jsonc;
            var reader = new AprBeta6Reader();
            var records = reader.ReadStream(await File.ReadAllTextAsync(input), representation);
            var forms = records.OfType<AprFormRecord>().ToList();
            if (forms.Count != 1)
            {
                Console.Error.WriteLine("Error: attest requires exactly one subject form in its input stream.");
                return 1;
            }
            var password = Option(args, "--password=");
            var fields = Option(args, "--fields=")?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            // The platform default is temporary key storage; macOS does not support
            // EphemeralKeySet through the PKCS#12 loader.
            using var certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath, password);
            var attestation = AprAttestationFactory.Create(forms[0].Value, certificate, fields);
            await File.WriteAllTextAsync(output, reader.WriteStream(records.Append(attestation), representation));
            Console.WriteLine($"Appended beta.6 attestation to {output}.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"✗ APR beta.6 attestation error: {ex.Message}");
            return 1;
        }
    }

    private static string? Option(IEnumerable<string> args, string prefix) =>
        args.FirstOrDefault(value => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?[prefix.Length..];
}
