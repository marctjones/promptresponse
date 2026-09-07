using System.Text.Json;
using System.Text.Json.Nodes;
using PromptResponse.Core;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Expressions;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;

namespace PromptResponse.Cli.Commands;

/// <summary>Answers the conformance suite on standard input, as a driver.</summary>
/// <remarks>
/// The library's driver in <c>tools/</c> calls <c>PromptResponse.Core</c> directly, so a
/// passing run there says the library is conformant. It says nothing about the program a
/// person actually runs. This verb answers the same contract through the command line,
/// and the two are scored side by side.
///
/// Where they disagree, the difference is the finding: a defect in the library shows in
/// both, and a defect in the command line's own layer shows in one. Neither driver could
/// produce that signal alone.
///
///     apr conformance &lt; tests/Conformance/beta6/suite.json
///     python3 scripts/run-conformance.py --driver "apr conformance"
/// </remarks>
public sealed class ConformanceCommand : ICommand
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        var reader = new AprBeta6Reader();
        var validator = new DocumentValidator();
        var suite = JsonNode.Parse(await Console.In.ReadToEndAsync())?.AsObject();
        if (suite?["cases"] is not JsonArray cases)
        {
            Console.Error.WriteLine(
                "Usage: apr conformance < tests/Conformance/beta6/suite.json");
            return 1;
        }

        var results = new JsonArray();
        foreach (var node in cases)
        {
            results.Add(Answer(node!.AsObject(), reader, validator));
        }

        var report = new JsonObject
        {
            ["implementation"] = new JsonObject
            {
                ["name"] = "PromptResponse CLI",
                ["version"] = AprFormat.CurrentVersion,
                // The same claim the library driver makes. A claim is binding: every case
                // in a claimed profile must be answered, so claiming what this cannot
                // answer would be worse than claiming less.
                ["profiles"] = new JsonArray("core", "core+streams", "core+expressions"),
            },
            ["results"] = results,
        };
        Console.Out.Write(report.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
        return 0;
    }

    private static JsonObject Answer(
        JsonObject testCase, AprBeta6Reader reader, DocumentValidator validator)
    {
        var id = testCase["id"]!.GetValue<string>();
        var representation = testCase["representation"]!.GetValue<string>();
        var document = testCase["document"]!.GetValue<string>();
        var kind = representation.StartsWith("yaml", StringComparison.Ordinal)
            ? AprRepresentation.Yaml
            : AprRepresentation.Jsonc;
        var streamed = representation.EndsWith("-stream", StringComparison.Ordinal);

        IReadOnlyList<AprStreamRecord> records;
        try
        {
            records = reader.ReadStream(document, kind);
        }
        catch (SerializationException exception)
        {
            return new JsonObject
            {
                ["id"] = id,
                ["outcome"] = "reject",
                ["diagnostic"] = exception.Code ?? "PARSE_ERROR",
            };
        }

        var warnings = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var record in records.OfType<AprFormRecord>())
        {
            var result = validator.Validate(record.Form);
            if (!result.IsValid)
            {
                return new JsonObject
                {
                    ["id"] = id,
                    ["outcome"] = "reject",
                    ["diagnostic"] = result.Errors[0].ErrorCode ?? "REQUIRED_FIELD",
                };
            }
            foreach (var warning in result.Warnings)
            {
                if (warning.WarningCode is { } code) warnings.Add(code);
            }
        }

        var answer = new JsonObject { ["id"] = id, ["outcome"] = "valid" };
        if (!streamed && records.Count == 1)
        {
            answer["digest"] = AprSemanticDigest.Digest(records[0] switch
            {
                AprFormRecord form => form.Value,
                AprAttestationRecord attestation => attestation.Value,
                _ => default,
            });
        }
        answer["warnings"] = new JsonArray([.. warnings.Select(code => (JsonNode)code!)]);

        if (testCase["evaluates"]?.GetValue<bool>() == true || testCase["expects"] is not null)
        {
            answer["evaluated"] = Evaluate(records, testCase["evaluate"]);
        }
        if (testCase["roundTrip"]?.GetValue<bool>() == true)
        {
            answer["written"] = reader.WriteStream(records, kind);
        }
        return answer;
    }

    private static JsonObject Evaluate(IReadOnlyList<AprStreamRecord> records, JsonNode? inputs)
    {
        // `_now`, `_today` and `ctx` come from the case, never from the host clock: that
        // is what makes a form evaluate the same way twice.
        var supplied = inputs?.AsObject();
        var today = supplied?["_today"]?.GetValue<string>();
        var context = supplied?["ctx"]?.AsObject()?.ToDictionary(
            pair => pair.Key, pair => pair.Value?.ToString() ?? string.Empty, StringComparer.Ordinal);

        var responses = new JsonObject();
        var hidden = new JsonObject();
        var validation = new JsonObject();
        foreach (var record in records.OfType<AprFormRecord>())
        {
            FormExpressions.RecomputeComputedValues(record.Form, today, context);
            var environment = FormExpressions.BuildContext(record.Form, today, context);
            foreach (var prompt in FormExpressions.GetAllPrompts(record.Form))
            {
                if (prompt.Id is not { Length: > 0 } id) continue;
                if (prompt.Hints?.ExprHidden is { Length: > 0 })
                {
                    hidden[id] = FormExpressions.IsHidden(prompt, environment);
                }
                if (prompt.Hints?.ExprValidation is { Length: > 0 })
                {
                    validation[id] = FormExpressions.Validate(prompt, environment) ?? string.Empty;
                }
                if (prompt.Hints?.ExprValue is { Length: > 0 })
                {
                    responses[id] = prompt.Response ?? string.Empty;
                }
            }
        }
        return new JsonObject
        {
            ["responses"] = responses,
            ["hidden"] = hidden,
            ["validation"] = validation,
        };
    }
}
