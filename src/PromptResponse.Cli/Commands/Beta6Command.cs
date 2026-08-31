using PromptResponse.Core.Beta6;
using PromptResponse.Core.Serialization;
using System.Text.Json;

namespace PromptResponse.Cli.Commands;

/// <summary>Explicit beta.6 file operations, including representation conversion.</summary>
public sealed class Beta6Command : ICommand
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: apr beta6 <validate|inspect|normalize> <file> [--yaml|--jsonc] [--json] [--output=<file>]");
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
                Write(new { valid = true, records = records.Count }, args);
                return 0;
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
            if (action != "normalize")
            {
                Console.Error.WriteLine("Error: beta6 action must be validate, inspect, or normalize.");
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
