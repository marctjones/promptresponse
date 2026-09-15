using System.Text;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using PromptResponse.Host.Abstractions;

namespace PromptResponse.Cli.Commands;

/// <summary>Delivers a completed document to one submission target it names.</summary>
/// <remarks>
/// Delivery goes through <see cref="IDelivery"/> rather than an <c>HttpClient</c> this
/// command owns: submission is a host capability, and the seam is the one decided in
/// <c>docs/ARCHITECTURE.md</c>.
///
/// The command keeps the decisions that are its own — which target, and whether a person
/// has confirmed. A port never asks; a surface does.
/// </remarks>
public sealed class SubmitCommand(
    IAprSerializer serializer, DocumentValidator validator, IDelivery delivery) : ICommand
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: apr submit <file.aprf> [--url=https://… | --url=mailto:…] --yes");
            return 1;
        }

        var file = args[0];
        if (!File.Exists(file)) { Console.Error.WriteLine("Error: File not found."); return 1; }

        // Read once, as bytes: what gets sent below is exactly what is on disk, in
        // whichever representation the file already is. Re-serializing through
        // `serializer` (JSONC-only) would silently rewrite a YAML source to JSON on its
        // way out -- specification 5.2.1 says a receiver holds "the request body, byte
        // for byte: the stream as the client wrote it, already a valid APR file," which
        // reads as a promise about the sender's behaviour as much as the receiver's.
        var bytes = await File.ReadAllBytesAsync(file);
        var document = serializer.Deserialize(Encoding.UTF8.GetString(bytes));
        var result = validator.Validate(document);
        if (!result.IsValid)
        {
            Console.Error.WriteLine(
                $"Error: this document has {result.Errors.Count} structural error(s) and is not "
                + $"ready to submit. The first is {result.Errors[0].ErrorCode} at "
                + $"{result.Errors[0].PropertyPath}.");
            return 1;
        }

        var declared = document.Metadata.SubmissionUrls ?? [];
        var requested = args.FirstOrDefault(a => a.StartsWith("--url=", StringComparison.Ordinal))?[6..];
        // No automatic fallback. Where a document names one target the choice is
        // unambiguous; where it names several, choosing for somebody is choosing where
        // their answers go.
        var chosen = requested ?? (declared.Count == 1 ? declared[0].Url : null);
        if (chosen is null)
        {
            Console.Error.WriteLine(declared.Count == 0
                ? "Error: this document names no submission target. Give one with --url=…"
                : $"Error: this document names {declared.Count} targets. Choose one with --url=…");
            return 1;
        }
        var entry = declared.FirstOrDefault(target => string.Equals(target.Url, chosen, StringComparison.Ordinal));
        if (declared.Count > 0 && entry is null)
        {
            Console.Error.WriteLine("Error: --url must be one of the targets the document names.");
            return 1;
        }
        // A post entry is sent as a multipart form this command does not build, and an entry
        // of a kind it does not implement is not acted on (APR-MODEL-139).
        if (entry is not null && entry.Kind != SubmissionTarget.Put)
        {
            Console.Error.WriteLine(
                $"Error: '{chosen}' is a {entry.Kind ?? "kindless"} target. This command submits "
                + "to put targets and mailto addresses.");
            return 1;
        }
        if (!Uri.TryCreate(chosen, UriKind.Absolute, out var target) || !delivery.Supports(target))
        {
            Console.Error.WriteLine(
                $"Error: '{chosen}' is not a submission target this format defines. The "
                + "format defines a pre-signed HTTPS PUT and a mailto address.");
            return 1;
        }

        if (!args.Contains("--yes", StringComparer.Ordinal))
        {
            Console.Error.WriteLine(
                $"Will send {Path.GetFileName(file)} to {target}. Re-run with --yes to confirm.");
            return 2;
        }

        var delivered = await delivery.DeliverAsync(
            target, bytes, MediaTypeFor(file), Path.GetFileName(file));

        switch (delivered.Outcome)
        {
            case DeliveryOutcome.Delivered:
                Console.WriteLine(delivered.Detail);
                return 0;
            case DeliveryOutcome.Unavailable:
                // Not a failure of the document or the target. Saying which it is keeps
                // somebody from editing a file that was never the problem.
                Console.Error.WriteLine($"Not sent: {delivered.Detail}");
                return 3;
            default:
                Console.Error.WriteLine($"Not sent: {delivered.Detail}");
                return 1;
        }
    }

    private static string MediaTypeFor(string path) =>
        path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
            ? "application/vnd.apr+yaml"
            : "application/vnd.apr+json";
}
