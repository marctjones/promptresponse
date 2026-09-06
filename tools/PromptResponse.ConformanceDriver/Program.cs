// The .NET SDK's conformance driver.
//
// Reads tests/Conformance/beta6/suite.json on stdin and writes what it did on stdout,
// exactly as docs/SDK_CONFORMANCE.md specifies. It is a thin program over
// PromptResponse.Core: everything it reports comes from the shipped library, so a
// green run is a statement about the library rather than about this file.
//
// The suite arrives with its answers withheld. A case says what kind of answer is
// required — `evaluates`, `reportsWarnings`, `roundTrip` — and never what the answer
// is, so nothing here can be tuned to the expected result.
//
//     dotnet run --project tools/PromptResponse.ConformanceDriver < suite.json
//     python3 scripts/run-conformance.py --driver "dotnet run --project tools/PromptResponse.ConformanceDriver"

using System.Text.Json;
using System.Text.Json.Nodes;
using PromptResponse.Core;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Core.Validation;

var reader = new AprBeta6Reader();
var validator = new DocumentValidator();
var suite = JsonNode.Parse(await Console.In.ReadToEndAsync())!.AsObject();
var results = new JsonArray();

foreach (var node in suite["cases"]!.AsArray())
{
    var testCase = node!.AsObject();
    results.Add(Answer(testCase));
}

var report = new JsonObject
{
    ["implementation"] = new JsonObject
    {
        ["name"] = "PromptResponse.Core (.NET)",
        ["version"] = AprFormat.CurrentVersion,
        // Claimed deliberately, and binding: every case in a claimed profile must be
        // answered. Attestations and expressions are not claimed because the library
        // does not yet verify a proof or evaluate a hint through this driver, and a
        // claim this driver cannot answer is worse than no claim.
        ["profiles"] = new JsonArray("core", "core+streams"),
    },
    ["results"] = results,
};
Console.Out.Write(report.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
return 0;

JsonObject Answer(JsonObject testCase)
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
        records = streamed
            ? reader.ReadStream(document, kind)
            : [new AprFormRecord(reader.ReadForm(document, kind), ValueOf(document, kind))];
    }
    catch (SerializationException exception)
    {
        // A parse failure is a different class from a validation failure, and the
        // format says so. Report the code this document names for the condition where
        // it names one, and PARSE_ERROR where it does not.
        return new JsonObject
        {
            ["id"] = id,
            ["outcome"] = "reject",
            ["diagnostic"] = ParseDiagnostic(exception),
        };
    }

    var warnings = new SortedSet<string>(StringComparer.Ordinal);
    foreach (var record in records)
    {
        if (record is not AprFormRecord form) continue;
        var result = validator.Validate(form.Form);
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

    // The digest is how a case proves more than acceptance: it says the document was
    // read as the same document, which is where a reader with wrong scalar resolution
    // is caught. A stream has no single semantic model, so it states none.
    if (!streamed && records.Count == 1 && records[0] is AprFormRecord single)
    {
        answer["digest"] = AprSemanticDigest.Digest(single.Value);
    }

    answer["warnings"] = new JsonArray([.. warnings.Select(code => (JsonNode)code!)]);

    if (testCase["roundTrip"]?.GetValue<bool>() == true)
    {
        // Writing is serializing the semantic model. Nothing is filtered on the way
        // out, which is the whole of what preservation asks for.
        answer["written"] = streamed
            ? reader.WriteStream(records, kind)
            : reader.WriteForm(((AprFormRecord)records[0]).Form, kind);
    }

    return answer;
}

static JsonElement ValueOf(string document, AprRepresentation representation)
{
    // The reader exposes the parsed value on a stream record; a single form is read
    // through the same path so the digest is computed over one model, not two.
    var reader = new AprBeta6Reader();
    var records = reader.ReadStream(document, representation);
    return ((AprFormRecord)records[0]).Value;
}

static string ParseDiagnostic(SerializationException exception)
{
    // The library says which condition it refused for. Guessing from the message text
    // is how a reader ends up reporting YAML_TAG_FORBIDDEN for a directive, because the
    // directive message mentions %TAG. Where the specification names no code for the
    // condition, the parse stage's generic code is the answer (specification 7.3).
    return exception.Code ?? "PARSE_ERROR";
}
