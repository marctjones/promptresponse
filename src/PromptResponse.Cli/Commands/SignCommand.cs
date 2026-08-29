using System.Security.Cryptography.X509Certificates;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Signing;

namespace PromptResponse.Cli.Commands;

/// <summary>
/// Signs an APR document with an X.509 certificate (PKCS#12 / .pfx). A publisher
/// signs the template and binds the submission URL; a filler signs the responses
/// of specific fields.
/// </summary>
public class SignCommand : ICommand
{
    private readonly IAprSerializer _serializer;

    public SignCommand(IAprSerializer serializer) => _serializer = serializer;

    public async Task<int> ExecuteAsync(string[] args)
    {
        var filePath = args.FirstOrDefault(a => !a.StartsWith("--"));
        var certPath = GetArgValue(args, "--cert");
        var password = GetArgValue(args, "--password");
        var publisher = args.Contains("--publisher");
        var fieldsArg = GetArgValue(args, "--fields");
        var url = GetArgValue(args, "--url");
        var id = GetArgValue(args, "--id");
        var output = GetArgValue(args, "--output");

        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(certPath))
        {
            Console.Error.WriteLine("Error: an APR file and --cert are required");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  apr sign <file> --publisher --cert=<file.pfx> [--password=<pw>] --url=<submitUrl[,submitUrl...]> [--id=<id>] [--output=<file>]");
            Console.Error.WriteLine("  apr sign <file> --fields=<id1,id2,...> --cert=<file.pfx> [--password=<pw>] [--id=<id>] [--output=<file>]");
            return 1;
        }
        if (!File.Exists(filePath)) { Console.Error.WriteLine($"Error: File not found: {filePath}"); return 1; }
        if (!File.Exists(certPath)) { Console.Error.WriteLine($"Error: Certificate not found: {certPath}"); return 1; }
        if (!publisher && string.IsNullOrEmpty(fieldsArg))
        {
            Console.Error.WriteLine("Error: specify --publisher, or --fields=<id1,id2,...> to sign responses");
            return 1;
        }

        try
        {
            var document = _serializer.Deserialize(await File.ReadAllTextAsync(filePath));
            using var cert = SignatureCertificates.LoadPfx(certPath!, password);
            if (!cert.HasPrivateKey)
            {
                Console.Error.WriteLine("Error: the certificate has no private key (need a .pfx that includes the key)");
                return 1;
            }

            document.Signatures ??= new List<Signature>();
            Signature signature;

            if (publisher)
            {
                // The URL must be on the document BEFORE signing: the payload binds
                // what the document says, so setting it afterwards would sign one URL
                // and ship another.
                if (!string.IsNullOrEmpty(url)) document.Metadata.SubmissionUrls = url.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                signature = AprSigner.SignTemplate(document, cert, DateTime.UtcNow, id ?? "publisher");
                document.Metadata.Publisher ??= signature.Signer.Name;
            }
            else
            {
                var fields = fieldsArg!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                signature = AprSigner.SignFields(document, cert, fields, DateTime.UtcNow, id ?? $"sig{document.Signatures.Count + 1}");
            }

            document.Signatures.Add(signature);

            var outPath = string.IsNullOrEmpty(output) ? filePath! : output!;
            await File.WriteAllTextAsync(outPath, _serializer.Serialize(document));

            Console.WriteLine($"Signed as {signature.Role}: {signature.Signer.Name}");
            Console.WriteLine($"  Scope:      {(signature.Scope == "template" ? "template (form definition)" : string.Join(", ", signature.Fields))}");
            if (document.Metadata.SubmissionUrls is { Count: > 0 } bound) Console.WriteLine($"  Submit URLs: {string.Join(", ", bound)} (bound)");
            Console.WriteLine($"  Thumbprint: {signature.Signer.Thumbprint}");
            Console.WriteLine($"Wrote: {outPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: failed to sign: {ex.Message}");
            return 1;
        }
    }

    private static string? GetArgValue(string[] args, string prefix)
    {
        var arg = args.FirstOrDefault(a => a.StartsWith(prefix + "="));
        return arg?.Substring(prefix.Length + 1);
    }
}
