using PromptResponse.Core.Beta6;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;
using System.Text.Json;

namespace PromptResponse.Cli.Commands;

/// <summary>Explicit beta.6 file operations, including representation conversion.</summary>
public sealed class Beta6Command : ICommand
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: apr beta6 <validate|inspect|digest|normalize> <file> [--yaml|--jsonc] [--json] [--output=<file>]");
            return 1;
        }

        var action = args[0].ToLowerInvariant();
        var path = args[1];
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Error: File not found: {path}");
            return 1;
        }

        try
        {
            var source = await File.ReadAllTextAsync(path);
            var representation = InputRepresentationFor(path);
            var reader = new AprBeta6Reader();
            var records = reader.ReadStream(source, representation);
            if (action == "validate")
            {
                // This used to report `valid = true` for anything that parsed, which is
                // a different question. A document can parse cleanly and still say
                // something the format does not allow, and telling somebody their file
                // is valid when nothing checked it is worse than not checking.
                var validator = new DocumentValidator();
                var errors = new List<object>();
                var warnings = new List<object>();
                foreach (var form in records.OfType<AprFormRecord>())
                {
                    var result = validator.Validate(form.Form);
                    errors.AddRange(result.Errors.Select(error => (object)new
                    {
                        code = error.ErrorCode ?? "REQUIRED_FIELD",
                        path = error.PropertyPath,
                        message = error.Message,
                    }));
                    warnings.AddRange(result.Warnings.Select(warning => (object)new
                    {
                        code = warning.WarningCode,
                        path = warning.PropertyPath,
                        message = warning.Message,
                    }));
                }
                Write(new
                {
                    valid = errors.Count == 0,
                    records = records.Count,
                    // An advisory never affects validity, and reporting it beside the
                    // verdict is how a person learns what a reader will say about their
                    // document without being told the document is wrong.
                    errors,
                    warnings,
                }, args);
                return errors.Count == 0 ? 0 : 1;
            }
            if (action == "inspect")
            {
                var resolutions = AprAttestationResolver.Resolve(records);
                var report = new
                {
                    records = records.Count,
                    forms = records.OfType<AprFormRecord>().Count(),
                    attestations = resolutions.Select((resolution, index) => new
                    {
                        index,
                        state = resolution.State.ToString().ToLowerInvariant(),
                        differingPaths = resolution.DifferingPaths,
                        witnessesResolved = resolution.WitnessesResolved,
                    }),
                };
                Write(report, args);
                return 0;
            }
            if (action == "digest")
            {
                // The digest is the document's identity, and an integrity manifest is
                // that identity at every depth. Both are what an attestation is taken
                // over, so a person checking why a signature no longer verifies needs to
                // see them without writing a program.
                var report = records.Select((record, index) =>
                {
                    var value = record switch
                    {
                        AprFormRecord form => form.Value,
                        AprAttestationRecord attestation => attestation.Value,
                        _ => default,
                    };
                    var manifest = record is AprFormRecord manifested
                        ? AprSemanticDigest.CreateManifest(manifested.Value)
                        : null;
                    return new
                    {
                        index,
                        kind = record is AprAttestationRecord ? "attestation" : "form",
                        digest = AprSemanticDigest.Digest(value),
                        canonicalization = AprSemanticDigest.Canonicalization,
                        manifest = manifest is null ? null : new
                        {
                            root = manifest.Root,
                            entries = manifest.Entries.Select(entry => new
                            {
                                path = entry.Path,
                                digest = entry.Digest,
                            }),
                        },
                    };
                });
                Write(new { records = records.Count, results = report }, args);
                return 0;
            }
            if (action != "normalize")
            {
                Console.Error.WriteLine(
                    "Error: beta6 action must be validate, inspect, digest, or normalize.");
                return 1;
            }

            var output = args.FirstOrDefault(arg => arg.StartsWith("--output=", StringComparison.Ordinal))?[9..] ?? path;
            var outputRepresentation = OutputRepresentationFor(args, representation);
            await File.WriteAllTextAsync(output, reader.WriteStream(records, outputRepresentation));
            Console.WriteLine($"Normalized {records.Count} beta.6 record(s) to {output}.");
            return 0;
        }
        catch (SerializationException ex)
        {
            // A parse failure has its own vocabulary, distinct from the validation error
            // table: a document that will not parse was never validated. Reported as
            // machine-readable output for `validate`, because a caller scripting this
            // needs the code rather than the prose.
            if (action == "validate")
            {
                Write(new
                {
                    valid = false,
                    records = 0,
                    errors = new[] { new { code = ex.Code ?? "PARSE_ERROR", path = "", message = ex.Message } },
                    warnings = Array.Empty<object>(),
                }, args);
                return 1;
            }
            Console.Error.WriteLine($"✗ APR beta.6 error: {ex.Message}");
            return 1;
        }
    }

    private static AprRepresentation InputRepresentationFor(string path) =>
        path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
            ? AprRepresentation.Yaml : AprRepresentation.Jsonc;

    private static AprRepresentation OutputRepresentationFor(string[] args, AprRepresentation input) =>
        args.Contains("--yaml", StringComparer.OrdinalIgnoreCase) ? AprRepresentation.Yaml
        : args.Contains("--jsonc", StringComparer.OrdinalIgnoreCase) ? AprRepresentation.Jsonc
        : input;

    private static void Write<T>(T report, string[] args)
    {
        var options = args.Contains("--json", StringComparer.OrdinalIgnoreCase)
            ? null : new JsonSerializerOptions { WriteIndented = true };
        Console.WriteLine(JsonSerializer.Serialize(report, options));
    }
}
